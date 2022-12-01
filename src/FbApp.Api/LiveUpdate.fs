module FbApp.Api.LiveUpdate

open System
open System.Collections.Generic
open Dapr.Client
open FbApp.Api.Configuration
open FbApp.Api.Domain
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Quartz

[<DisallowConcurrentExecution>]
type LiveUpdateJob (authOptions: IOptions<AuthOptions>, dapr: DaprClient, logger: ILogger<LiveUpdateJob>) =
    let [<Literal>] StoreName = "fbapp-state"
    let [<Literal>] CompetitionId = 2000L

    let competitionKey id =
        $"competition-%d{id}"

    let fixtureKey id =
        $"fixture-%d{id}"

    let standingsKey competitionId group =
        $"standings-%d{competitionId}-%s{group}"

    let getRequestFilters isFullUpdate =
        if isFullUpdate then [] else
        let today = DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero)
        [FootballData.CompetitionFixtureFilter.Range(today, today)]

    let updateFixturesHandler (evt: FixturesUpdatedIntegrationEvent) = task {
        logger.LogInformation("Updating fixtures domain state")

        let mapGoals (value: (int * int) option) : Fixtures.FixtureGoals option =
            value |> Option.map (fun (home, away) -> { Home = home; Away = away })

        for fixture in evt.Fixtures do
            let competitionGuid = Competitions.createId fixture.CompetitionId
            let maybeCommand =
                match fixture.Stage with
                | "GROUP_STAGE" ->
                    Fixtures.UpdateFixture {
                        Status = fixture.Status
                        FullTime = mapGoals fixture.FullTime
                        ExtraTime = mapGoals fixture.ExtraTime
                        Penalties = mapGoals fixture.Penalties
                        }
                    |> Some
                | "LAST_16" | "QUARTER_FINALS" | "SEMI_FINALS" | "THIRD_PLACE" | "FINAL" ->
                    match fixture.HomeTeamId, fixture.AwayTeamId with
                    | Some(homeTeamId), Some(awayTeamId) ->
                        Fixtures.UpdateQualifiers {
                            CompetitionId = competitionGuid
                            ExternalId = fixture.FixtureId
                            HomeTeamId = homeTeamId
                            AwayTeamId = awayTeamId
                            Date = fixture.UtcDate
                            Stage = fixture.Stage
                            Status = fixture.Status
                            FullTime = mapGoals fixture.FullTime
                            ExtraTime = mapGoals fixture.ExtraTime
                            Penalties = mapGoals fixture.Penalties
                            }
                        |> Some
                    | _ ->
                        None
                | _ ->
                    logger.LogError($"Unknown stage value for fixture %A{id}: %s{fixture.Stage}")
                    None

            match maybeCommand with
            | Some command ->
                let fixtureId = Fixtures.createId (competitionGuid, fixture.FixtureId)
                match! CommandHandlers.fixturesHandler (fixtureId, Aggregate.Any) command with
                | Ok _ ->
                    ()
                | Error err ->
                    logger.LogError($"Failed to update fixture %A{id}: %A{err}")
            | None ->
                ()
    }

    let updateFixtures isFullUpdate cancellationToken = task {
        match! FootballData.getCompetitionFixtures authOptions.Value.FootballDataToken CompetitionId (getRequestFilters isFullUpdate)  with
        | Ok matches ->
            logger.LogInformation("Loaded data of {count} fixtures.", matches.Fixtures.Length)

            let! fixtureUpdatesLookup, tag =
                dapr.GetStateAndETagAsync<Dictionary<int64, DateTimeOffset>>(
                    StoreName,
                    competitionKey CompetitionId,
                    cancellationToken = cancellationToken
                )

            let fixtureUpdatesLookup =
                match fixtureUpdatesLookup with
                | null -> Dictionary<int64, DateTimeOffset>()
                | value -> value

            let isUpdated (fixture: FootballData.CompetitionFixture) =
                match fixtureUpdatesLookup.TryGetValue fixture.Id with
                | false, v | true, v when v < fixture.LastUpdated -> true
                | _ -> false

            let fixtureUpdates = ResizeArray<FixtureDto * string>()

            let mapGoals (g: FootballData.FixtureScore option) =
                let homeTeam, awayTeam = g |> Option.map (fun x -> (x.Home, x.Away)) |> Option.defaultValue (None, None)
                match homeTeam, awayTeam with
                | Some(home), Some(away) -> Some(home, away)
                | _ -> None

            for fixture in matches.Fixtures |> Array.filter isUpdated do
                let! previousFixture, tag =
                    dapr.GetStateAndETagAsync<FixtureDto>(
                        StoreName,
                        fixtureKey fixture.Id,
                        cancellationToken = cancellationToken
                    )

                let newFixture: FixtureDto = {
                    FixtureId = fixture.Id
                    CompetitionId = CompetitionId
                    HomeTeamId = fixture.HomeTeam |> Option.bind (fun x -> x.Id)
                    AwayTeamId = fixture.AwayTeam |> Option.bind (fun x -> x.Id)
                    UtcDate = fixture.Date
                    Stage = fixture.Stage
                    Status = fixture.Status
                    FullTime = mapGoals (fixture.Result |> Option.map (fun x -> x.FullTime))
                    HalfTime = mapGoals (fixture.Result |> Option.map (fun x -> x.HalfTime))
                    ExtraTime = mapGoals (fixture.Result |> Option.bind (fun x -> x.ExtraTime))
                    Penalties = mapGoals (fixture.Result |> Option.bind (fun x -> x.Penalties))
                    Winner = fixture.Result |> Option.bind (fun x -> x.Winner)
                    Duration = fixture.Result |> Option.map (fun x -> x.Duration) |> Option.defaultValue "REGULAR"
                }

                if newFixture <> previousFixture then
                    logger.LogInformation("Updating fixture {FixtureId} state updated", fixture.Id)
                    fixtureUpdates.Add((newFixture, tag))

                fixtureUpdatesLookup[fixture.Id] <- fixture.LastUpdated

            if fixtureUpdates.Count > 0 then
                do! updateFixturesHandler { Fixtures = fixtureUpdates |> Seq.map fst |> Seq.toArray }
                let! _ =
                    dapr.TrySaveStateAsync(
                        StoreName,
                        competitionKey CompetitionId,
                        fixtureUpdatesLookup,
                        tag,
                        cancellationToken = cancellationToken
                    )
                for f, t in fixtureUpdates do
                    let! _ = dapr.TrySaveStateAsync(StoreName, fixtureKey f.FixtureId, f, t, cancellationToken = cancellationToken)
                    ()

        | Error (errorCode, reason, error) ->
            logger.LogCritical("API returned error code {ErrorCode} ({Reason}): {Error}", errorCode, reason, error)
    }

    let updateGroupTable cancellationToken (standings: FootballData.CompetitionLeagueTableStandings) = task {
        let key = standingsKey CompetitionId standings.Group

        let! previousHashCode, tag =
            dapr.GetStateAndETagAsync<string>(StoreName, key, cancellationToken = cancellationToken)

        let bytes = snd (Serialization.serialize(standings))
        let currentHash = Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(bytes.ToArray()))

        if previousHashCode = currentHash then () else

        if standings.Table |> Array.exists (fun x -> x.PlayedGames > 0) then
            let rows =
                standings.Table
                |> Seq.map (fun x ->
                    let r: Competitions.StandingRow = {
                        Position = x.Position
                        TeamId = x.Team.Id
                        PlayedGames = x.PlayedGames
                        Won = x.Won
                        Draw = x.Draw
                        Lost = x.Lost
                        GoalsFor = x.GoalsFor
                        GoalsAgainst = x.GoalsAgainst
                    }
                    r
                )
                |> Seq.toList

            let command = Competitions.Command.UpdateStandings (standings.Group, rows)

            let competitionId = Competitions.createId 2000L
            match! CommandHandlers.competitionsHandler (competitionId, Aggregate.Any) command with
            | Ok _ ->
                ()
            | Error err ->
                logger.LogError($"Failed to update fixture %A{id}: %A{err}")

        let! _ = dapr.TrySaveStateAsync(StoreName, key, currentHash, tag, cancellationToken = cancellationToken)
        ()
    }

    let updateStandings cancellationToken = task {
        match! FootballData.getCompetitionLeagueTable authOptions.Value.FootballDataToken CompetitionId with
        | Ok competition ->
            for group in competition.Standings |> Array.filter (fun x -> x.Stage = "GROUP_STAGE") do
                do! updateGroupTable cancellationToken group
        | Error (errorCode, reason, error) ->
            logger.LogCritical("API returned error code {ErrorCode} ({Reason}): {Error}", errorCode, reason, error)
    }

    interface IJob with
        member _.Execute context = task {
            try
                logger.LogInformation("{Job} job executing, triggered by {Trigger} at {Time}", context.JobDetail.Key, context.Trigger.Key, DateTimeOffset.UtcNow)
                let isFullUpdate = context.MergedJobDataMap.GetBoolean("fullUpdate")
                logger.LogInformation("Performing {Type} update of fixture data", if isFullUpdate then "full" else "partial")
                do! updateFixtures isFullUpdate context.CancellationToken
                logger.LogInformation("Fixture update completed at {Time}", DateTimeOffset.UtcNow)
                logger.LogInformation("Updating competition standings")
                do! updateStandings context.CancellationToken
                logger.LogInformation("Standings update completed at {Time}", DateTimeOffset.UtcNow)
            with e ->
                logger.LogError(e, "Exception occured while updating fixtures.")
        }

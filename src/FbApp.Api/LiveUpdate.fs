module FbApp.Api.LiveUpdate

open System
open System.Collections.Generic
open FbApp.Api.Configuration
open FbApp.Api.Domain
open FbApp.Common.SimpleTypes
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Quartz


type Posix =
    private Posix of int64


module Posix =
    let create (value: DateTimeOffset) =
        Posix (value.ToUnixTimeSeconds())

    let value (Posix posix) =
        posix


module InMemoryCache =
    let Fixtures = Dictionary<FixtureId, string>()
    let FixtureUpdates = Dictionary<FixtureId, Posix>()
    let Standings = Dictionary<string, string>()


module Hash =
    let calculate (o: obj) =
        let bytes = snd (Serialization.serialize(o))
        Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(bytes.ToArray()))


[<DisallowConcurrentExecution>]
type LiveUpdateJob (authOptions: IOptions<AuthOptions>, logger: ILogger<LiveUpdateJob>) =
    let [<Literal>] CompetitionApiId =
        2000L

    let competitionId =
        CompetitionId.create CompetitionApiId

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
            let fixtureId = FixtureId.create competitionId fixture.FixtureId
            let maybeCommand =
                match Fixtures.FixtureStage.tryFromString fixture.Stage with
                | Some Fixtures.Group ->
                    Fixtures.UpdateFixture {
                        Status = fixture.Status
                        FullTime = mapGoals fixture.FullTime
                        ExtraTime = mapGoals fixture.ExtraTime
                        Penalties = mapGoals fixture.Penalties
                        }
                    |> Some
                | Some (Fixtures.Last16 | Fixtures.QuarterFinals | Fixtures.SemiFinals | Fixtures.ThirdPlace | Fixtures.Final) ->
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
                    logger.LogError($"Unknown stage value for fixture %A{fixtureId}: %s{fixture.Stage}")
                    None

            match maybeCommand with
            | Some command ->
                let fixtureId = Fixtures.createId (competitionGuid, fixture.FixtureId)
                match! CommandHandlers.fixturesHandler (fixtureId, Aggregate.Any) command with
                | Ok _ ->
                    ()
                | Error err ->
                    logger.LogError($"Failed to update fixture %A{fixtureId}: %A{err}")
            | None ->
                ()
    }

    let updateFixtures isFullUpdate _ = task {
        match! FootballData.getCompetitionFixtures authOptions.Value.FootballDataToken CompetitionApiId (getRequestFilters isFullUpdate)  with
        | Ok matches ->
            logger.LogInformation("Loaded data of {count} fixtures.", matches.Fixtures.Length)

            let isUpdated (fixture: FootballData.CompetitionFixture) =
                let fixtureId = FixtureId.create competitionId fixture.Id
                match InMemoryCache.FixtureUpdates.TryGetValue fixtureId with
                | true, lastUpdatePosix -> Posix.value lastUpdatePosix < fixture.LastUpdated.ToUnixTimeSeconds()
                | _ -> true

            let fixtureUpdates = ResizeArray<FixtureDto>()

            let mapGoals (g: FootballData.FixtureScore option) =
                let homeTeam, awayTeam = g |> Option.map (fun x -> (x.Home, x.Away)) |> Option.defaultValue (None, None)
                match homeTeam, awayTeam with
                | Some(home), Some(away) -> Some(home, away)
                | _ -> None

            let updateCache = ResizeArray<_>()

            for fixture in matches.Fixtures |> Array.filter isUpdated do
                let fixtureId = FixtureId.create competitionId fixture.Id

                let previousFixtureHashCode =
                    match InMemoryCache.Fixtures.TryGetValue(fixtureId) with
                    | true, hashCode -> Some(hashCode)
                    | _ -> None

                let newFixture: FixtureDto = {
                    FixtureId = fixture.Id
                    CompetitionId = CompetitionApiId
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

                let currentFixtureHashCode = Hash.calculate newFixture

                if Some(currentFixtureHashCode) <> previousFixtureHashCode then
                    logger.LogInformation("Updating fixture {FixtureId} state updated", fixture.Id)
                    fixtureUpdates.Add(newFixture)

                updateCache.Add(fun () ->
                    InMemoryCache.Fixtures[fixtureId] <- currentFixtureHashCode
                    InMemoryCache.FixtureUpdates[fixtureId] <- Posix.create fixture.LastUpdated
                )

            if fixtureUpdates.Count > 0 then
                do! updateFixturesHandler { Fixtures = fixtureUpdates |> Seq.toArray }

            updateCache |> Seq.iter (fun f -> f())

        | Error (errorCode, reason, error) ->
            logger.LogCritical("API returned error code {ErrorCode} ({Reason}): {Error}", errorCode, reason, error)
    }

    let updateGroupTable _ (standings: FootballData.CompetitionLeagueTableStandings) = task {
        let key = standingsKey CompetitionApiId standings.Group

        let previousHashCode =
            match InMemoryCache.Standings.TryGetValue(key) with
            | true, hashCode -> Some(hashCode)
            | _ -> None

        let currentHashCode = Hash.calculate standings

        if previousHashCode = Some(currentHashCode) then () else

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
                logger.LogError($"Failed to update competition standings in group %s{standings.Group}: %A{err}")

        InMemoryCache.Standings[key] <- currentHashCode

        ()
    }

    let updateStandings cancellationToken = task {
        match! FootballData.getCompetitionLeagueTable authOptions.Value.FootballDataToken CompetitionApiId with
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

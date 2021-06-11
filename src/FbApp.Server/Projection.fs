module FbApp.Server.Projection

open EventStore.Client
open FbApp.Core.EventStore
open FbApp.Core.Serialization
open FbApp.Domain
open FbApp.Server.Repositories
open FSharp.Control.Tasks
open Microsoft.Extensions.Logging
open MongoDB.Driver
open System
open System.Collections.Generic

let projectCompetitions (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Competitions.Event> (e.Event.EventType, e.Event.Data) with
    | Competitions.Created args ->
        try
            let competitionModel: ReadModels.Competition =
                {
                    Id =  md.AggregateId
                    Description = args.Description
                    ExternalId = args.ExternalId
                    Teams = [||]
                    Fixtures = [||]
                    Groups = Dictionary<_,_>()
                    Version = md.AggregateSequenceNumber
                    Date = args.Date.ToOffset(TimeSpan.Zero)
                }
            do! Competitions.insert competitionModel
        with
            | :? MongoWriteException as ex ->
                log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Competitions.TeamsAssigned teams ->
        let teamProjections =
            teams
            |> List.map
                (fun team ->
                    {
                        Name = team.Name
                        Code = team.Code
                        FlagUrl = team.FlagUrl
                        ExternalId = team.ExternalId
                    } : ReadModels.Team)
            |> List.toArray
        do! Competitions.updateTeams (md.AggregateId, md.AggregateSequenceNumber) teamProjections

    | Competitions.FixturesAssigned fixtures ->
        let fixtureProjections =
            fixtures |> List.map (fun t ->
                {
                    HomeTeamId = t.HomeTeamId
                    AwayTeamId = t.AwayTeamId
                    Date = t.Date.ToOffset(TimeSpan.Zero)
                    ExternalId = t.ExternalId
                } : ReadModels.CompetitionFixture
            ) |> List.toArray
        do! Competitions.updateFixtures (md.AggregateId, md.AggregateSequenceNumber) fixtureProjections

    | Competitions.GroupsAssigned groups ->
        do! (dict groups) |> Competitions.updateGroups (md.AggregateId, md.AggregateSequenceNumber)
}

let (|Nullable|_|) (x: Nullable<_>) = if x.HasValue then Some(x.Value) else None

let private mapActualResult (status, fullTime, extraTime: int array, penalties: int array) =
    match status, fullTime with
    | "FINISHED", [| homeGoals; awayGoals |] ->
        let et = if extraTime |> isNull then [| 0; 0 |] else extraTime
        let ps = if penalties |> isNull then [| 0; 0 |] else penalties
        let homeGoals = homeGoals + et.[0] + ps.[0]
        let awayGoals = awayGoals + et.[1] + ps.[1]
        if homeGoals > awayGoals then "HomeWin"
        elif homeGoals < awayGoals then "AwayWin"
        else "Tie"
    | _ -> null

let private mapFixtureResult competitionId (x: Predictions.FixtureResultRegistration) = task {
    let mapResult = function
        | Predictions.HomeWin -> "HomeWin"
        | Predictions.Tie -> "Tie"
        | Predictions.AwayWin -> "AwayWin"

    let fixtureId = Fixtures.createId (competitionId, x.Id)
    let! result = Fixtures.getFixtureStatus fixtureId

    let result = mapActualResult (result.Status, result.FullTime, result.ExtraTime, result.Penalties)

    let fixtureResult : ReadModels.PredictionFixtureResult =
        {
            FixtureId = x.Id
            PredictedResult = mapResult x.Result
            ActualResult = result
        }

    return fixtureResult
}

let projectPredictions (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Predictions.Event> (e.Event.EventType, e.Event.Data) with
    | Predictions.Registered args ->
        try
            let fixtureTasks =
                args.Fixtures
                |> Seq.map (mapFixtureResult args.CompetitionId)
                |> Seq.toArray

            let! fixtures = System.Threading.Tasks.Task.WhenAll(fixtureTasks)

            let updates =
                fixtures
                |> Array.map
                    (fun fixture -> task {
                        let prediction: ReadModels.FixtureResultPrediction =
                            {
                                PredictionId = md.AggregateId
                                Name = args.Name
                                Result = fixture.PredictedResult
                            }
                        let id = Fixtures.createId (args.CompetitionId, fixture.FixtureId)
                        do! Fixtures.addPrediction id prediction
                    })

            let! _ = System.Threading.Tasks.Task.WhenAll(updates)

            let prediction: ReadModels.Prediction =
                {
                    Id = md.AggregateId
                    Name = args.Name
                    Email = args.Email
                    CompetitionId = args.CompetitionId
                    Fixtures = fixtures
                    QualifiersRoundOf16 = args.Qualifiers.RoundOf16 |> List.map (fun x -> { Id = x; HasQualified = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
                    QualifiersRoundOf8 = args.Qualifiers.RoundOf8 |> List.map (fun x -> { Id = x; HasQualified = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
                    QualifiersRoundOf4 = args.Qualifiers.RoundOf4 |> List.map (fun x -> { Id = x; HasQualified = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
                    QualifiersRoundOf2 = args.Qualifiers.RoundOf2 |> List.map (fun x -> { Id = x; HasQualified = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
                    Winner = { Id = args.Winner; HasQualified = Nullable() }
                    Leagues = [||]
                    Version = md.AggregateSequenceNumber
                }
            do! Predictions.insert prediction
        with
            | :? MongoWriteException as ex ->
                log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Predictions.Declined ->
        do! Predictions.delete md.AggregateId
}

let updateFixtureOrder competitionId = task {
    let! fixtures = Fixtures.getFixtureOrder competitionId
    match fixtures |> Seq.toList with
    | [] | [_] -> ()
    | [x;y] ->
        if x.PreviousId.HasValue || x.NextId <> Nullable(y.Id) then
            do! Fixtures.setAdjacentFixtures x.Id (None, Some(y.Id))
        if y.PreviousId <> Nullable(x.Id) || y.NextId.HasValue then
            do! Fixtures.setAdjacentFixtures y.Id (Some(x.Id), None)
    | fixtures ->
        let x, y = match fixtures with x::y::_ -> x, y | _ -> failwith "never"
        if x.PreviousId.HasValue || x.NextId <> Nullable(y.Id) then
            do! Fixtures.setAdjacentFixtures x.Id (None, Some(y.Id))
        for w in fixtures |> List.windowed 3 do
            let p, x, n = match w with [p; x; n] -> p, x, n | _ -> failwith "never"
            if x.PreviousId <> Nullable(p.Id) || x.NextId <> Nullable(n.Id) then
                do! Fixtures.setAdjacentFixtures x.Id (Some(p.Id), Some(n.Id))
        let x, y = match fixtures |> List.rev with x::y::_ -> x, y | _ -> failwith "never"
        if x.PreviousId <> Nullable(y.Id) || x.NextId.HasValue then
            do! Fixtures.setAdjacentFixtures x.Id (Some(y.Id), None)
}

let getResultPredictions (input: Fixtures.AddFixtureInput) = task {
    if input.Stage <> "GROUP_STAGE" then return [||] else
    let! predictions = Predictions.ofFixture input.CompetitionId input.ExternalId
    let predictions =
        predictions
        |> Seq.map
            (fun x ->
                {
                    PredictionId = x.Id
                    Name = x.Name
                    Result = x.Fixtures.[0].PredictedResult
                } : ReadModels.FixtureResultPrediction)
        |> Seq.toArray
    return predictions
}

let getQualificationPredictions (input: Fixtures.AddFixtureInput) = task {
    if input.Stage = "GROUP_STAGE" then return [||] else
    let! predictions = Predictions.ofStage (input.CompetitionId, input.Stage)
    let result =
        predictions
        |> Seq.map (fun x ->
            {
                PredictionId = x.Id
                Name = x.Name
                HomeQualifies = x.Qualifiers |> Array.exists (fun z -> z.Id = input.HomeTeamId)
                AwayQualifies = x.Qualifiers |> Array.exists (fun z -> z.Id = input.AwayTeamId)
            } : ReadModels.QualificationPrediction)
        |> Seq.toArray
    return result
}

let updateQualifiedTeams (competition: ReadModels.Competition) = task {
    let! qualifiedTeams = Fixtures.getQualifiedTeams competition.Id
    let unqualifiedTeams = competition.Teams |> Seq.map (fun x -> x.ExternalId) |> Seq.except qualifiedTeams |> Seq.toArray
    do! Predictions.setUnqualifiedTeams (competition.Id, unqualifiedTeams)
}

let updateScore (fixtureId: Guid, expectedVersion: int64, fullTime, extraTime, penalties) = task {
    let! fixture = Fixtures.get fixtureId
    do! Fixtures.updateScore (fixtureId, expectedVersion) (fullTime, extraTime, penalties)
    let ps = match penalties with Some(u) -> [| u.Home; u.Away |] | _ -> [| 0; 0 |]
    let et = match extraTime with Some(u) -> [| u.Home; u.Away |] | _ -> [| 0; 0 |]
    let actualResult = mapActualResult (fixture.Status, [| fullTime.Home; fullTime.Away |], et, ps)
    if fixture.Stage = "GROUP_STAGE" then
        if actualResult |> isNull |> not then
            do! Predictions.updateResult (fixture.CompetitionId, fixture.ExternalId, actualResult)
    else if fixture.Status = "FINISHED" then
        do! Predictions.updateQualifiers (fixture.CompetitionId, fixture.Stage, fixture.HomeTeam.ExternalId, actualResult = "HomeWin")
        do! Predictions.updateQualifiers (fixture.CompetitionId, fixture.Stage, fixture.AwayTeam.ExternalId, actualResult = "AwayWin")
}

let projectFixtures (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Fixtures.Event> (e.Event.EventType, e.Event.Data) with
    | Fixtures.Added input ->
        try
            let! competition = Competitions.get input.CompetitionId
            let competition = competition |> Option.get

            let homeTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.HomeTeamId)
            let awayTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.AwayTeamId)

            let! resultPredictions = getResultPredictions input
            let! qualificationPredictions = getQualificationPredictions input

            let stage = if input.Stage |> String.IsNullOrEmpty then "GROUP_STAGE" else input.Stage

            let fixtureModel: ReadModels.Fixture =
                {
                    Id = md.AggregateId
                    CompetitionId = input.CompetitionId
                    Date = input.Date.ToOffset(TimeSpan.Zero)
                    PreviousId = Nullable()
                    NextId = Nullable()
                    HomeTeam = homeTeam
                    AwayTeam = awayTeam
                    Status = input.Status
                    FullTime = null
                    ExtraTime = null
                    Penalties = null
                    ResultPredictions = resultPredictions
                    QualificationPredictions = qualificationPredictions
                    ExternalId = input.ExternalId
                    Stage = stage
                    Version = md.AggregateSequenceNumber
                }

            do! Fixtures.insert fixtureModel
            do! updateFixtureOrder input.CompetitionId

            if stage <> "GROUP_STAGE" then
                do! Predictions.updateQualifiers (input.CompetitionId, "GROUP_STAGE", fixtureModel.HomeTeam.ExternalId, true)
                do! Predictions.updateQualifiers (input.CompetitionId, "GROUP_STAGE", fixtureModel.AwayTeam.ExternalId, true)

            if stage = "ROUND_OF_16" then
                let! numFixtures = Fixtures.getFixtureCount (input.CompetitionId, "ROUND_OF_16")
                if numFixtures = 8L then
                    do! updateQualifiedTeams competition

        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Fixtures.ScoreChanged (homeGoals, awayGoals) ->
        do! updateScore (md.AggregateId, md.AggregateSequenceNumber, { Home = homeGoals; Away = awayGoals }, None, None)

    | Fixtures.ScoreChanged2 { FullTime = fullTime; ExtraTime = extraTime; Penalties = penalties } ->
        do! updateScore (md.AggregateId, md.AggregateSequenceNumber, fullTime, extraTime, penalties)

    | Fixtures.StatusChanged status ->
        let! fixture = Fixtures.get md.AggregateId
        do! Fixtures.updateStatus (md.AggregateId, md.AggregateSequenceNumber) status
        let actualResult = mapActualResult (status.ToString(), fixture.FullTime, fixture.ExtraTime, fixture.Penalties)
        if fixture.Stage = "GROUP_STAGE" then
            if actualResult |> isNull |> not then
                do! Predictions.updateResult (fixture.CompetitionId, fixture.ExternalId, actualResult)
        else if status = Fixtures.Finished then
            do! Predictions.updateQualifiers (fixture.CompetitionId, fixture.Stage, fixture.HomeTeam.ExternalId, actualResult = "HomeWin")
            do! Predictions.updateQualifiers (fixture.CompetitionId, fixture.Stage, fixture.AwayTeam.ExternalId, actualResult = "AwayWin")
}

let projectLeagues (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Leagues.Event> (e.Event.EventType, e.Event.Data) with
    | Leagues.Created input ->
        try
            let leagueModel : ReadModels.League =
                {
                    Id = md.AggregateId
                    CompetitionId = input.CompetitionId
                    Code = input.Code
                    Name = input.Name
                }
            do! Leagues.insert leagueModel
        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Leagues.PredictionAdded predictionId ->
        do! Predictions.addToLeague (predictionId, md.AggregateId)
}

let eventAppeared (log: ILogger) (e: ResolvedEvent) = unitTask {
    try
        match getMetadata e with
        | Some(md) when md.AggregateName = Competitions.AggregateName ->
            do! projectCompetitions log md e
        | Some(md) when md.AggregateName = Predictions.AggregateName ->
            do! projectPredictions log md e
        | Some(md) when md.AggregateName = Fixtures.AggregateName ->
            do! projectFixtures log md e
        | Some(md) when md.AggregateName = Leagues.AggregateName ->
            do! projectLeagues log md e
        | _ -> ()
    with ex ->
        log.LogError(ex, "Projection error of event {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
        raise ex
}

type private Marker = class end

let connectSubscription (client: EventStoreClient) (loggerFactory: ILoggerFactory) = unitTask {
    let log = loggerFactory.CreateLogger(typeof<Marker>.DeclaringType)
    let! _ = client.SubscribeToStreamAsync(EventStore.DomainEventsStreamName, fun _ e _ -> eventAppeared log e)
    ()
}

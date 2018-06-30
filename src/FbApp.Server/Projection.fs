module FbApp.Server.Projection

open EventStore.ClientAPI
open FbApp.Core.EventStore
open FbApp.Core.Serialization
open FbApp.Domain
open FbApp.Server.Repositories
open FSharp.Control.Tasks.ContextInsensitive
open Microsoft.Extensions.Logging
open MongoDB.Driver
open System
open System.Collections.Generic
open FbApp.Domain

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

let private mapActualResult (status, homeGoals, awayGoals) =
    match status, homeGoals, awayGoals with
    | "FINISHED", Nullable(homeGoals), Nullable(awayGoals) ->
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

    let result = mapActualResult (result.Status, result.HomeGoals, result.AwayGoals)

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
                        let prediction: ReadModels.FixturePrediction =
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
                    QualifiersRoundOf16 = args.Qualifiers.RoundOf16 |> List.map (fun x -> { Id = x; Score = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
                    QualifiersRoundOf8 = args.Qualifiers.RoundOf8 |> List.map (fun x -> { Id = x; Score = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
                    QualifiersRoundOf4 = args.Qualifiers.RoundOf4 |> List.map (fun x -> { Id = x; Score = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
                    QualifiersRoundOf2 = args.Qualifiers.RoundOf2 |> List.map (fun x -> { Id = x; Score = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
                    Winner = { Id = args.Winner; Score = Nullable() }
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

let projectFixtures (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Fixtures.Event> (e.Event.EventType, e.Event.Data) with
    | Fixtures.Added input ->
        try
            let! competition = Competitions.get input.CompetitionId
            let competition = competition |> Option.get

            let homeTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.HomeTeamId)
            let awayTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.AwayTeamId)

            let! predictions = Predictions.ofFixture competition.Id input.ExternalId
            let predictions =
                predictions
                |> Seq.map
                    (fun x ->
                        {
                            PredictionId = x.Id
                            Name = x.Name
                            Result = x.Fixtures.[0].PredictedResult
                        } : ReadModels.FixturePrediction)
                |> Seq.toArray

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
                    HomeGoals = Nullable()
                    AwayGoals = Nullable()
                    Predictions = predictions
                    ExternalId = input.ExternalId
                    Version = md.AggregateSequenceNumber
                }

            do! Fixtures.insert fixtureModel
            do! updateFixtureOrder input.CompetitionId

        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Fixtures.ScoreChanged (homeGoals, awayGoals) ->
        let! fixture = Fixtures.get md.AggregateId
        do! Fixtures.updateScore (md.AggregateId, md.AggregateSequenceNumber) (homeGoals, awayGoals)
        let actualResult = mapActualResult (fixture.Status, Nullable(homeGoals), Nullable(awayGoals))
        if actualResult |> isNull |> not then
            do! Predictions.updateResult (fixture.CompetitionId, fixture.ExternalId, actualResult)

    | Fixtures.StatusChanged status ->
        let! fixture = Fixtures.get md.AggregateId
        do! Fixtures.updateStatus (md.AggregateId, md.AggregateSequenceNumber) status
        let actualResult = mapActualResult (status.ToString(), fixture.HomeGoals, fixture.AwayGoals)
        if actualResult |> isNull |> not then
            do! Predictions.updateResult (fixture.CompetitionId, fixture.ExternalId, actualResult)
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

let eventAppeared (log: ILogger) (subscription: EventStorePersistentSubscriptionBase) (e: ResolvedEvent) : System.Threading.Tasks.Task = upcast task {
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
        subscription.Acknowledge(e)
    with ex ->
        log.LogError(ex, "Projection error of event {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
        subscription.Fail(e, PersistentSubscriptionNakEventAction.Retry, "unexpected exception occured")
}

type private Marker = class end

let connectSubscription (connection: IEventStoreConnection) (loggerFactory: ILoggerFactory) =
    let log = loggerFactory.CreateLogger(typeof<Marker>.DeclaringType)
    connection.ConnectToPersistentSubscription(EventStore.DomainEventsStreamName, EventStore.ProjectionsSubscriptionGroup, (eventAppeared log), autoAck = false) |> ignore

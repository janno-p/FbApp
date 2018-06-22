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

let projectPredictions (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Predictions.Event> (e.Event.EventType, e.Event.Data) with
    | Predictions.Registered args ->
        try
            let mapResult = function
                | Predictions.HomeWin -> "HomeWin"
                | Predictions.Tie -> "Tie"
                | Predictions.AwayWin -> "AwayWin"

            let fixtures =
                args.Fixtures
                |> Seq.map (fun x -> { FixtureId = x.Id; Result = (mapResult x.Result) } : ReadModels.PredictionFixtureResult)
                |> Seq.toArray

            let updates =
                fixtures
                |> Array.map
                    (fun fixture -> task {
                        let prediction: ReadModels.FixturePrediction =
                            {
                                PredictionId = md.AggregateId
                                Name = args.Name
                                Result = fixture.Result
                            }
                        let id = Fixtures.Id (args.CompetitionId, fixture.FixtureId) |> Fixtures.streamId
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
                    QualifiersRoundOf16 = args.Qualifiers.RoundOf16 |> List.toArray
                    QualifiersRoundOf8 = args.Qualifiers.RoundOf8 |> List.toArray
                    QualifiersRoundOf4 = args.Qualifiers.RoundOf4 |> List.toArray
                    QualifiersRoundOf2 = args.Qualifiers.RoundOf2 |> List.toArray
                    Winner = args.Winner
                    Version = md.AggregateSequenceNumber
                }
            do! Predictions.insert prediction
        with
            | :? MongoWriteException as ex ->
                log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Predictions.Declined ->
        do! Predictions.delete md.AggregateId
}

let trySetNextFixture (competitionId: Guid) (fixtureId: Guid) (date: DateTimeOffset) = task {
    let! nextFixtures = Fixtures.findNext competitionId date
    match nextFixtures |> Seq.toList with
    | f1::f2::_ | _::f1::f2::_ when f1.Id = fixtureId ->
        do! Fixtures.setPreviousFixture f2.Id fixtureId
        return f2.PreviousId, Nullable(f2.Id)
    | _ ->
        return Nullable(), Nullable()
}

let trySetPreviousFixture (competitionId: Guid) (fixtureId: Guid) (date: DateTimeOffset) = task {
    let! previousFixtures = Fixtures.findPrevious competitionId date
    match previousFixtures |> Seq.toList with
    | f1::f2::_ | _::f1::f2::_ when f1.Id = fixtureId ->
        do! Fixtures.setNextFixture f2.Id fixtureId
        return Nullable(f2.Id), f2.NextId
    | _ ->
        return! trySetNextFixture competitionId fixtureId date
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
                            Result = x.Fixtures.[0].Result
                        } : ReadModels.FixturePrediction)
                |> Seq.toArray

            let date = input.Date.ToOffset(TimeSpan.Zero)

            let! previousFixture, nextFixture =
                trySetPreviousFixture input.CompetitionId md.AggregateId date

            let fixtureModel: ReadModels.Fixture =
                {
                    Id = md.AggregateId
                    CompetitionId = input.CompetitionId
                    Date = input.Date.ToOffset(TimeSpan.Zero)
                    PreviousId = previousFixture
                    NextId = nextFixture
                    HomeTeam = homeTeam
                    AwayTeam = awayTeam
                    Status = input.Status
                    HomeGoals = Nullable()
                    AwayGoals = Nullable()
                    Predictions = predictions
                    Version = md.AggregateSequenceNumber
                }

            do! Fixtures.insert fixtureModel

        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Fixtures.ScoreChanged (homeGoals, awayGoals) ->
        do! Fixtures.updateScore (md.AggregateId, md.AggregateSequenceNumber) (homeGoals, awayGoals)

    | Fixtures.StatusChanged status ->
        do! Fixtures.updateStatus (md.AggregateId, md.AggregateSequenceNumber) status
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

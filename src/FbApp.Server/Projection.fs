module FbApp.Server.Projection

open EventStore.ClientAPI
open FbApp.Core.EventStore
open FbApp.Core.Serialization
open FbApp.Domain
open FSharp.Control.Tasks.ContextInsensitive
open Microsoft.Extensions.Logging
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open System
open System.Collections.Generic

module Projections =
    type Team =
        {
            Name: string
            Code: string
            FlagUrl: string
            ExternalId: int64
        }

    type CompetitionFixture =
        {
            HomeTeamId: int64
            AwayTeamId: int64
            Date: DateTimeOffset
            ExternalId: int64
        }

    type Competition =
        {
            [<BsonId>] Id: Guid
            Description: string
            ExternalId: int64
            Teams: Team[]
            Fixtures: CompetitionFixture[]
            Groups: IDictionary<string, int64[]>
            Version: int64
            Date: DateTimeOffset
        }

    type FixtureResult =
        {
            FixtureId: int64
            Result: string
        }

    type Prediction =
        {
            [<BsonId>] Id: Guid
            Name: string
            Email: string
            CompetitionId: Guid
            Fixtures: FixtureResult[]
            QualifiersRoundOf16: int64[]
            QualifiersRoundOf8: int64[]
            QualifiersRoundOf4: int64[]
            QualifiersRoundOf2: int64[]
            Winner: int64
            Version: int64
        }

    type FixturePrediction =
        {
            PredictionId: Guid
            Name: string
            Result: string
        }

    [<CLIMutable>]
    type Fixture =
        {
            Id: Guid
            CompetitionId: Guid
            Date: DateTimeOffset
            HomeTeam: Team
            AwayTeam: Team
            Status: string
            HomeGoals: Nullable<int>
            AwayGoals: Nullable<int>
            Predictions: FixturePrediction[]
            Version: int64
        }

let mongo = MongoClient()
let db = mongo.GetDatabase("fbapp")
let competitions = db.GetCollection<Projections.Competition>("competitions")
let predictions = db.GetCollection<Projections.Prediction>("predictions")
let fixtures = db.GetCollection<Projections.Fixture>("fixtures")

let competitionIdFilter (id, ver) =
    Builders<Projections.Competition>.Filter.Where(fun x -> x.Id = id && x.Version = ver - 1L)

module FindFluent =
    let trySingleAsync (x: IFindFluent<_,_>) = task {
        let! result = x.SingleOrDefaultAsync()
        return (if result |> box |> isNull then None else Some(result))
    }

let getActiveCompetition () = task {
    let f = Builders<Projections.Competition>.Filter.Eq((fun x -> x.ExternalId), 467L)
    return! competitions.Find(f).Limit(Nullable(1)).SingleAsync()
}

let getCompetition (competitionId: Guid) = task {
    let f = Builders<Projections.Competition>.Filter.Eq((fun x -> x.Id), competitionId)
    return! competitions.Find(f).Limit(Nullable(1)) |> FindFluent.trySingleAsync
}

let projectCompetitions (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Competitions.Event> (e.Event.EventType, e.Event.Data) with
    | Competitions.Created args ->
        try
            let competitionModel: Projections.Competition =
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
            let! _ = competitions.InsertOneAsync(competitionModel)
            ()
        with
            | :? MongoWriteException as ex ->
                log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Competitions.TeamsAssigned teams ->
        let teamProjections =
                teams |> List.map (fun t ->
                {
                    Name = t.Name
                    Code = t.Code
                    FlagUrl = t.FlagUrl
                    ExternalId = t.ExternalId
                } : Projections.Team
                ) |> List.toArray
        let u = Builders<Projections.Competition>.Update
                    .Set((fun x -> x.Version), md.AggregateSequenceNumber)
                    .Set((fun x -> x.Teams), teamProjections)
        let! _ = competitions.UpdateOneAsync(competitionIdFilter (md.AggregateId, md.AggregateSequenceNumber), u)
        ()

    | Competitions.FixturesAssigned fixtures ->
        let fixtureProjections =
            fixtures |> List.map (fun t ->
                {
                    HomeTeamId = t.HomeTeamId
                    AwayTeamId = t.AwayTeamId
                    Date = t.Date.ToOffset(TimeSpan.Zero)
                    ExternalId = t.ExternalId
                } : Projections.CompetitionFixture
            ) |> List.toArray
        let u = Builders<Projections.Competition>.Update
                    .Set((fun x -> x.Version), md.AggregateSequenceNumber)
                    .Set((fun x -> x.Fixtures), fixtureProjections)
        let! _ = competitions.UpdateOneAsync(competitionIdFilter (md.AggregateId, md.AggregateSequenceNumber), u)
        ()

    | Competitions.GroupsAssigned groups ->
        let groupProjections = groups |> dict
        let u = Builders<Projections.Competition>.Update
                    .Set((fun x -> x.Version), md.AggregateSequenceNumber)
                    .Set((fun x -> x.Groups), groupProjections)
        let! _ = competitions.UpdateOneAsync(competitionIdFilter (md.AggregateId, md.AggregateSequenceNumber), u)
        ()
}

let projectPredictions (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Predictions.Event> (e.Event.EventType, e.Event.Data) with
    | Predictions.Registered args ->
        try
            let mapResult = function
                | Predictions.HomeWin -> "HomeWin"
                | Predictions.Tie -> "Tie"
                | Predictions.AwayWin -> "AwayWin"
            let predictionModel: Projections.Prediction =
                {
                    Id = md.AggregateId
                    Name = args.Name
                    Email = args.Email
                    CompetitionId = args.CompetitionId
                    Fixtures = args.Fixtures |> Seq.map (fun x -> { FixtureId = x.Id; Result = (mapResult x.Result) } : Projections.FixtureResult) |> Seq.toArray
                    QualifiersRoundOf16 = args.Qualifiers.RoundOf16 |> List.toArray
                    QualifiersRoundOf8 = args.Qualifiers.RoundOf8 |> List.toArray
                    QualifiersRoundOf4 = args.Qualifiers.RoundOf4 |> List.toArray
                    QualifiersRoundOf2 = args.Qualifiers.RoundOf2 |> List.toArray
                    Winner = args.Winner
                    Version = md.AggregateSequenceNumber
                }
            let! _ = predictions.InsertOneAsync(predictionModel)
            ()
        with
            | :? MongoWriteException as ex ->
                log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Predictions.Declined ->
        let f = Builders<Projections.Prediction>.Filter.Eq((fun x -> x.Id), md.AggregateId)
        let! _ = predictions.DeleteOneAsync(f)
        ()
}

let projectFixtures (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Fixtures.Event> (e.Event.EventType, e.Event.Data) with
    | Fixtures.Added input ->
        try
            let! competition = getCompetition input.CompetitionId
            let competition = competition |> Option.get

            let homeTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.HomeTeamId)
            let awayTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.AwayTeamId)

            let fixtureModel: Projections.Fixture =
                {
                    Id = md.AggregateId
                    CompetitionId = input.CompetitionId
                    Date = input.Date.ToOffset(TimeSpan.Zero)
                    HomeTeam = homeTeam
                    AwayTeam = awayTeam
                    Status = input.Status
                    HomeGoals = Nullable()
                    AwayGoals = Nullable()
                    Predictions = [||]
                    Version = md.AggregateSequenceNumber
                }
            let! _ = fixtures.InsertOneAsync(fixtureModel)
            ()
        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Fixtures.ScoreChanged (homeGoals, awayGoals) ->
        let f = Builders<Projections.Fixture>.Filter.Eq((fun x -> x.Id), md.AggregateId)
        let u = Builders<Projections.Fixture>.Update
                    .Set((fun x -> x.Version), md.AggregateSequenceNumber)
                    .Set((fun x -> x.HomeGoals), Nullable(homeGoals))
                    .Set((fun x -> x.AwayGoals), Nullable(awayGoals))
        let! _ = fixtures.UpdateOneAsync(f, u)
        ()
    | Fixtures.StatusChanged status ->
        let f = Builders<Projections.Fixture>.Filter.Eq((fun x -> x.Id), md.AggregateId)
        let u = Builders<Projections.Fixture>.Update
                    .Set((fun x -> x.Version), md.AggregateSequenceNumber)
                    .Set((fun x -> x.Status), status.ToString())
        let! _ = fixtures.UpdateOneAsync(f, u)
        ()
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

type private X = class end

let connectSubscription (connection: IEventStoreConnection) (loggerFactory: ILoggerFactory) =
    let log = loggerFactory.CreateLogger(typeof<X>.DeclaringType)
    connection.ConnectToPersistentSubscription(EventStore.DomainEventsStreamName, EventStore.ProjectionsSubscriptionGroup, (eventAppeared log), autoAck = false) |> ignore

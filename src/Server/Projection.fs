module FbApp.Server.Projection

open EventStore.ClientAPI
open FSharp.Control.Tasks.ContextInsensitive
open Giraffe
open Microsoft.Extensions.Logging
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open System

module Projections =
    type Team =
        {
            Name: string
            Code: string
            FlagUrl: string
            ExternalId: int64
        }

    type Fixture =
        {
            HomeTeamId: int64
            AwayTeamId: int64
            Date: DateTime
            ExternalId: int64
        }

    type Competition =
        {
            [<BsonId>] Id: Guid
            Description: string
            ExternalSource: int64
            Teams: Team[]
            Fixtures: Fixture[]
            Version: int64
        }

let mongo = MongoClient()
let db = mongo.GetDatabase("fbapp")
let competitions = db.GetCollection("competitions")

let competitionIdFilter (id, ver) =
    Builders<Projections.Competition>.Filter.Where(fun x -> x.Id = id && x.Version = ver - 1L)

let eventAppeared (log: ILogger) (subscription: EventStorePersistentSubscriptionBase) (e: ResolvedEvent) : System.Threading.Tasks.Task = upcast task {
    try
        match EventStore.getMetadata e with
        | Some(md) when md.AggregateName = "Competition" ->
            match Serialization.deserializeOf<Competition.Event> (e.Event.EventType, e.Event.Data) with
            | Competition.Created args ->
                try
                    let competitionModel: Projections.Competition =
                        {
                            Id =  md.AggregateId
                            Description = args.Description
                            ExternalSource = args.ExternalSource
                            Teams = [||]
                            Fixtures = [||]
                            Version = md.AggregateSequenceNumber
                        }
                    let! _ = competitions.InsertOneAsync(competitionModel)
                    ()
                with
                    | :? MongoWriteException as ex ->
                        log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

            | Competition.TeamsAssigned teams ->
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

            | Competition.FixturesAssigned fixtures ->
                let fixtureProjections =
                    fixtures |> List.map (fun t ->
                        {
                            HomeTeamId = t.HomeTeamId
                            AwayTeamId = t.AwayTeamId
                            Date = t.Date
                            ExternalId = t.ExternalId
                        } : Projections.Fixture
                    ) |> List.toArray
                let u = Builders<Projections.Competition>.Update
                            .Set((fun x -> x.Version), md.AggregateSequenceNumber)
                            .Set((fun x -> x.Fixtures), fixtureProjections)
                let! _ = competitions.UpdateOneAsync(competitionIdFilter (md.AggregateId, md.AggregateSequenceNumber), u)
                ()
        | _ -> ()
    with ex ->
        log.LogError(ex, "Projection error of event {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
        raise ex
}

type private X = class end

let connectSubscription (connection: IEventStoreConnection) (loggerFactory: ILoggerFactory) =
    let log = loggerFactory.CreateLogger(typeof<X>.DeclaringType)
    connection.ConnectToPersistentSubscription("$ce-Competition", "projections", (eventAppeared log)) |> ignore

module FbApp.Server.Projection

open EventStore.ClientAPI
open FSharp.Control.Tasks.ContextInsensitive
open Giraffe
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

let eventAppeared (subscription: EventStorePersistentSubscriptionBase) (e: ResolvedEvent) : System.Threading.Tasks.Task = upcast task {
    try
        if e.Event |> isNotNull && not (e.Event.EventStreamId.StartsWith("$$")) then
            let md: EventStore.Metadata = Serialization.deserializeType e.Event.Metadata
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
                    | :? MongoWriteException as e -> printfn "Already exists: %A" e

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
    with ex ->
        printfn "Error in projections (%s): %A" e.Event.EventStreamId ex
        raise ex
}

let connectSubscription (connection: IEventStoreConnection) =
    connection.ConnectToPersistentSubscription("$ce-Competition", "projections", eventAppeared) |> ignore

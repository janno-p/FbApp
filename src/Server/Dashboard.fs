module FbApp.Server.Dashboard

open EventStore.ClientAPI
open EventStore.ClientAPI
open EventStore.ClientAPI
open FSharp.Control.Tasks
open FSharp.Control.Tasks
open FbApp.Server.Common
open FSharp.Control.Tasks.ContextInsensitive
open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server
open Giraffe
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open MongoDB.Driver
open Newtonsoft.Json
open Saturn
open System
open System.Net.Http

type CompetitionItem =
    {
        Label: string
        Value: int
    }

type CompetitionDto =
    {
        Description: string
        ExternalSource: int64
    }

let connection =
    EventStore.connect().Result

let handleCommand =
    Aggregate.makeHandler
        { InitialState = Competition.initialState; Decide = Competition.decide; Evolve = Competition.evolve }
        (EventStore.makeRepository connection "Competition" Serialization.serialize Serialization.deserialize)

type CompetitionTeamReadModel =
    {
        Name: string
        Code: string
        FlagUrl: string
        ExternalId: int64
    }

type CompetitionFixtureReadModel =
    {
        HomeTeamId: int64
        AwayTeamId: int64
        Date: DateTime
        ExternalId: int64
    }

type CompetitionReadModel =
    {
        [<BsonId>] Id: Guid
        Description: string
        ExternalSource: int64
        Teams: CompetitionTeamReadModel[]
        Fixtures: CompetitionFixtureReadModel[]
        Version: int64
    }

let mongo = MongoClient()
let db = mongo.GetDatabase("fbapp")
let competitions = db.GetCollection("competitions")

let eventAppeared (subscription: EventStorePersistentSubscriptionBase) (e: ResolvedEvent) : System.Threading.Tasks.Task = upcast task {
    try
        let md: EventStore.Metadata = Serialization.deserializeType e.Event.Metadata
        match Serialization.deserializeOf<Competition.Event> (e.Event.EventType, e.Event.Data) with
        | Competition.Created args ->
            try
                let competitionModel =
                    {
                        Id =  md.AggregateId
                        Description = args.Description
                        ExternalSource = args.ExternalSource
                        Teams = [||]
                        Fixtures = [||]
                        Version = md.AggregateSequenceNumber
                    }
                let! _ = competitions.InsertOneAsync(competitionModel)
                let! teams = FootballData.loadCompetitionTeams args.ExternalSource
                let teamsCommand = Competition.Command.AssignTeams (teams |> List.ofArray)
                let! version = handleCommand (md.AggregateId, md.AggregateSequenceNumber) teamsCommand
                match version with
                | Ok(v) ->
                    let! fixtures = FootballData.loadCompetitionFixtures args.ExternalSource
                    let fixturesCommand = Competition.Command.AssignFixtures (fixtures |> List.ofArray)
                    let! _ = handleCommand (md.AggregateId, v) fixturesCommand
                    ()
                | _ -> ()
            with
                | :? MongoWriteException as e -> printfn "Already exists: %A" e
        | Competition.TeamsAssigned teams ->
            let f = Builders<CompetitionReadModel>.Filter.Where(fun x -> x.Id = md.AggregateId && x.Version = (md.AggregateSequenceNumber - 1L))
            let u = Builders<CompetitionReadModel>.Update
                        .Set((fun x -> x.Version), md.AggregateSequenceNumber)
                        .Set((fun x -> x.Teams), teams |> List.map (fun t -> { Name = t.Name; Code = t.Code; FlagUrl = t.FlagUrl; ExternalId = t.ExternalId }) |> List.toArray)
            let! _ = competitions.UpdateOneAsync(f, u)
            ()
        | Competition.FixturesAssigned fixtures ->
            let f = Builders<CompetitionReadModel>.Filter.Where(fun x -> x.Id = md.AggregateId && x.Version = (md.AggregateSequenceNumber - 1L))
            let u = Builders<CompetitionReadModel>.Update
                        .Set((fun x -> x.Version), md.AggregateSequenceNumber)
                        .Set((fun x -> x.Fixtures), fixtures |> List.map (fun t -> { HomeTeamId = t.HomeTeamId; AwayTeamId = t.AwayTeamId; Date = t.Date; ExternalId = t.ExternalId }) |> List.toArray)
            let! _ = competitions.UpdateOneAsync(f, u)
            ()
    with e ->
        printfn "Error fixtures: %A" e
        raise e
}

let t = task {
    //connection.CreatePersistentSubscriptionAsync("$ce-Competition", "sync-competitions-read-model", PersistentSubscriptionSettings.Create().Build(), null)
    //|> ignore
    connection.ConnectToPersistentSubscription("$ce-Competition", "sync-competitions-read-model", eventAppeared)
    |> ignore
}

t.Wait()

let getCompetitionSources year: HttpHandler =
    (fun next context ->
        task {
            if year < 2016 then
                return! Successful.OK [||] next context
            else
                let! competitions = FootballData.loadCompetitionsOf year
                let competitions = competitions |> Array.map (fun x -> { Label = sprintf "%s (%s)" x.Caption x.League; Value = x.Id })
                return! Successful.OK competitions next context
        })

let addCompetition: HttpHandler =
    (fun next context ->
        task {
            let! dto = context.BindJsonAsync<CompetitionDto>()
            let command = Competition.Create(dto.Description, dto.ExternalSource)
            let id = Guid.NewGuid()
            let! result = handleCommand (id, 0L) command
            match result with
            | Ok(_) -> return! Successful.ACCEPTED (id.ToString("N")) next context
            | Error(_) -> return! RequestErrors.BAD_REQUEST "" next context
        })

let getCompetitions: HttpHandler =
    (fun next context ->
        task {
            let sort = Builders<CompetitionReadModel>.Sort.Ascending(FieldDefinition<CompetitionReadModel>.op_Implicit("Description"))
            let! competitions = competitions.FindAsync((fun _ -> true), FindOptions<_>(Sort = sort))
            let! competitions = competitions.ToListAsync()
            return! Successful.OK competitions next context
        })

let dashboardScope = scope {
    get "/competitions" getCompetitions
    getf "/competition_sources/%i" getCompetitionSources
    post "/competition/add" addCompetition
}

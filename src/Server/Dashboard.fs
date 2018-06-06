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
open FbApp.Server.Projection
open Giraffe
open Microsoft.Extensions.DependencyInjection
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open MongoDB.Driver
open Newtonsoft.Json
open Saturn
open System
open System.Net.Http
open System.Threading.Tasks

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
            let! result = Aggregate.Handlers.competitionHandler (id, 0L) command
            match result with
            | Ok(_) -> return! Successful.ACCEPTED (id.ToString("N")) next context
            | Error(_) -> return! RequestErrors.BAD_REQUEST "" next context
        })

let getCompetitions: HttpHandler =
    (fun next context ->
        task {
            let sort = Builders<Projections.Competition>.Sort.Ascending(FieldDefinition<Projections.Competition>.op_Implicit("Description"))
            let! competitions = competitions.FindAsync((fun _ -> true), FindOptions<_>(Sort = sort))
            let! competitions = competitions.ToListAsync()
            return! Successful.OK competitions next context
        })

let dashboardScope = scope {
    get "/competitions" getCompetitions
    getf "/competition_sources/%i" getCompetitionSources
    post "/competition/add" addCompetition
}

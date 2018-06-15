module FbApp.Server.Dashboard

open EventStore.ClientAPI
open FbApp.Core
open FbApp.Server.Configuration
open FbApp.Server
open FbApp.Server.Projection
open Giraffe
open MongoDB.Driver
open Saturn
open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options

type CompetitionItem =
    {
        Label: string
        Value: int64
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
                let authOptions = context.RequestServices.GetService<IOptions<AuthOptions>>().Value
                let! competitions = FootballData.getCompetitions authOptions.FootballDataToken [FootballData.Season year]
                match competitions with
                | Ok(competitions) ->
                    let competitions = competitions |> Array.map (fun x -> { Label = sprintf "%s (%s)" x.Caption x.League; Value = x.Id })
                    return! Successful.OK competitions next context
                | Error(_,_,err) ->
                    return! RequestErrors.BAD_REQUEST err.Error next context
        })

let addCompetition: HttpHandler =
    (fun next context ->
        task {
            let! dto = context.BindJsonAsync<CompetitionDto>()
            let command = Competitions.Create(dto.Description, dto.ExternalSource)
            let id = Guid.NewGuid()
            let! result = CommandHandlers.competitionsHandler (id, Aggregate.New) command
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

module FbApp.Api.Dashboard

open FbApp.Api
open FbApp.Api.Configuration
open FbApp.Api.Domain
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open MongoDB.Driver

type CompetitionItem =
    {
        Label: string
        Value: int64
    }

let getCompetitionSources year: HttpHandler =
    (fun next context ->
        task {
            if year < 2016 then
                return! Successful.OK [||] next context
            else
                let authOptions = context.RequestServices.GetService<IOptions<AuthOptions>>().Value
                let! competitions = FootballData.getCompetitions authOptions.FootballDataToken // [FootballData.Season year]
                match competitions with
                | Ok(competitions) ->
                    let competitions = competitions |> Array.map (fun x -> { Label = x.Name; Value = x.Id })
                    return! Successful.OK competitions next context
                | Error(_,_,err) ->
                    return! RequestErrors.BAD_REQUEST err.Error next context
        })

let addCompetitionDom input = task {
    let command = Competitions.Create input
    let id = Competitions.createId input.ExternalId
    return! CommandHandlers.competitionsHandler (id, Aggregate.New) command
}

let addCompetition: HttpHandler =
    (fun next context ->
        task {
            let! input = context.BindJsonAsync<Competitions.CreateInput>()
            match! addCompetitionDom input with
            | Ok _ -> return! Successful.ACCEPTED id next context
            | Error _ -> return! RequestErrors.CONFLICT "Competition already exists" next context
        })

let getCompetitions: HttpHandler =
    (fun next context ->
        task {
            let! competitions = Repositories.Competitions.getAll (context.RequestServices.GetRequiredService<IMongoDatabase>())
            return! Successful.OK competitions next context
        })

let dashboardScope: Endpoint list = [
    GET [
        route "/competitions" getCompetitions
        routef "/competition_sources/%i" getCompetitionSources
    ]
    POST [
        route "/competition/add" addCompetition
    ]
]

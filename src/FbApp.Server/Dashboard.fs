module FbApp.Server.Dashboard

open FbApp.Core
open FbApp.Domain
open FbApp.Server.Configuration
open FbApp.Server
open FSharp.Control.Tasks
open Giraffe
open Saturn
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options

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
                let! competitions = FootballData.Api2.getCompetitions authOptions.FootballDataToken // [FootballData.Season year]
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
            let! input = context.BindJsonAsync<Competitions.CreateInput>()
            let command = Competitions.Create input
            let id = Competitions.createId input.ExternalId
            let! result = CommandHandlers.competitionsHandler (id, Aggregate.New) command
            match result with
            | Ok(_) -> return! Successful.ACCEPTED id next context
            | Error(_) -> return! RequestErrors.CONFLICT "Competition already exists" next context
        })

let getCompetitions: HttpHandler =
    (fun next context ->
        task {
            let! competitions = Repositories.Competitions.getAll ()
            return! Successful.OK competitions next context
        })

let dashboardScope = router {
    get "/competitions" getCompetitions
    getf "/competition_sources/%i" getCompetitionSources
    post "/competition/add" addCompetition
}

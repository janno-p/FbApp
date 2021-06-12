module FbApp.Competitions.Api


open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open System


type GetActiveCompetitionApi = HttpHandler
type GetCompetitionSourcesApi = int -> HttpHandler


module AuthOptions =
    let footballDataToken (ctx: HttpContext) =
        ctx.RequestServices.GetRequiredService<IConfiguration>().["Authentication:FootballDataToken"]


let getActiveCompetitionApi: GetActiveCompetitionApi =
    fun next ctx ->
        //let dto: ActiveCompetitionDto =
        //    {
        //    Id = Guid.NewGuid()
        //    Name = "Jalgpalli EM - EURO 2020"
        //    Status = ""
        //    }
        //Successful.OK dto
        RequestErrors.NOT_FOUND "" next ctx


let getCompetitionSourcesApi: GetCompetitionSourcesApi =
    fun year ->
        fun next ctx -> task {
            if year < (DateTime.Now.Year - 5) then
                return! Successful.OK [||] next ctx
            else
                let token = AuthOptions.footballDataToken ctx
                match! FootballData.getCompetitions token [FootballData.Season year] with
                | Ok competitions ->
                    let competitions = competitions |> Array.map (fun x -> { Label = sprintf "%s (%s)" x.Caption x.League; Value = x.Id })
                    return! Successful.OK competitions next ctx
                | Error (_, _, err) ->
                    return! RequestErrors.BAD_REQUEST err.Error next ctx
        }

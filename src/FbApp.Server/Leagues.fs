module FbApp.Server.Leagues

open FbApp.Core
open FbApp.Domain
open FSharp.Control.Tasks
open Giraffe
open Saturn
open System

let private getLeague (code: string) : HttpHandler =
    (fun next ctx -> task {
        return! Successful.OK null next ctx
    })

let private getDefaultLeague : HttpHandler =
    (fun next ctx -> task {
        return! Successful.OK null next ctx
    })

let private addLeague : HttpHandler =
    (fun next ctx -> task {
        let! input = ctx.BindJsonAsync<Leagues.CreateLeagueInput>()
        let id = Leagues.createId (input.CompetitionId, input.Code)
        let! result = CommandHandlers.leaguesHandler (id, Aggregate.New) (Leagues.Create input)
        match result with
        | Ok(_) -> return! Successful.ACCEPTED id next ctx
        | Error(_) -> return! RequestErrors.CONFLICT "League already exists" next ctx
    })

let private addPrediction (leagueId: string, predictionId: string) : HttpHandler =
    (fun next ctx -> task {
        let leagueId = Guid.Parse(leagueId)
        let predictionId = Guid.Parse(predictionId)
        let! result = CommandHandlers.leaguesHandler (leagueId, Aggregate.Any) (Leagues.AddPrediction predictionId)
        match result with
        | Ok(_) -> return! Successful.ACCEPTED predictionId next ctx
        | Error(e) -> return! RequestErrors.BAD_REQUEST e next ctx
    })

let private getLeagues : HttpHandler =
    (fun next ctx -> task {
        let! leagues = Repositories.Leagues.getAll ()
        return! Successful.OK leagues next ctx
    })

let scope = router {
    get "/" getDefaultLeague
    getf "/league/%s" getLeague

    forward "/admin" (router {
        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        get "/" getLeagues

        post "/" addLeague
        postf "/%s/%s" addPrediction
    })
}

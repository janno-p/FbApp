﻿module FbApp.Server.Leagues

open FbApp.Core
open FbApp.Domain
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

let private addPrediction (leagueId: Guid, predictionId: Guid) : HttpHandler =
    (fun next ctx -> task {
        let! result = CommandHandlers.leaguesHandler (leagueId, Aggregate.Any) (Leagues.AddPrediction predictionId)
        match result with
        | Ok(_) -> return! Successful.ACCEPTED predictionId next ctx
        | Error(e) -> return! RequestErrors.BAD_REQUEST e next ctx
    })

let private getLeagues : HttpHandler =
    (fun next ctx -> task {
        return! Successful.OK [] next ctx
    })

let scope = scope {
    get "/" getDefaultLeague
    getf "/%s" getLeague

    forward "/admin" (scope {
        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        get "/" getLeagues

        post "/" addLeague
        postf "/%O/%O" addPrediction
    })
}

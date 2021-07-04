﻿module FbApp.Api.Predictions

open FbApp.Api.Repositories
open FSharp.Control.Tasks
open Giraffe
open Saturn
open Saturn.Endpoint

let private fixName (name: string) =
    name.Split([|' '|], 2).[0]

let getScoreTable : HttpHandler =
    (fun next ctx -> task {
        match! Competitions.tryGetActive () with
        | Some competition ->
            let! scoreTable = Predictions.getScoreTable competition.Id
            let scoreTable = scoreTable |> Seq.map (fun x -> { x with Name = fixName x.Name }) |> Seq.toArray
            return! Successful.OK scoreTable next ctx
        | None ->
            return! RequestErrors.NOT_FOUND "No active competition" next ctx
    })

let findPredictions term : HttpHandler =
    (fun next ctx -> task {
        match! Competitions.tryGetActive () with
        | Some competition ->
            let! predictions = Predictions.find competition.Id term
            return! Successful.OK predictions next ctx
        | None ->
            return! RequestErrors.NOT_FOUND "No active competition" next ctx
    })

let scope = router {
    get "/score" getScoreTable

    forward "/admin" (router {
        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        getf "/search/%s" findPredictions
    })
}

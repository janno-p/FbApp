module FbApp.Api.Predictions

open FbApp.Api.Repositories
open Giraffe
open Microsoft.Extensions.DependencyInjection
open MongoDB.Driver
open Saturn
open Saturn.Endpoint

let private fixName (name: string) =
    name.Split([|' '|], 2).[0]

let getScoreTable : HttpHandler =
    (fun next ctx -> task {
        let db = ctx.RequestServices.GetRequiredService<IMongoDatabase>()
        match! Competitions.tryGetActive db with
        | Some competition ->
            let! scoreTable = Predictions.getScoreTable db competition.Id
            let scoreTable = scoreTable |> Seq.map (fun x -> { x with Name = fixName x.Name }) |> Seq.toArray
            return! Successful.OK scoreTable next ctx
        | None ->
            return! RequestErrors.NOT_FOUND "No active competition" next ctx
    })

let findPredictions term : HttpHandler =
    (fun next ctx -> task {
        let db = ctx.RequestServices.GetRequiredService<IMongoDatabase>()
        match! Competitions.tryGetActive db with
        | Some competition ->
            let! predictions = Predictions.find db competition.Id term
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

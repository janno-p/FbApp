module FbApp.Api.Predictions

open FbApp.Api.Repositories
open Giraffe
open Microsoft.Extensions.DependencyInjection
open MongoDB.Driver
open Saturn
open Saturn.Endpoint


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
    forward "/admin" (router {
        pipe_through Auth.mustBeLoggedIn
        pipe_through Auth.mustBeAdmin

        getf "/search/%s" findPredictions
    })
}

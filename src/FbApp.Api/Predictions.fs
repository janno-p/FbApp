module FbApp.Api.Predictions

open FbApp.Api.Repositories
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.Extensions.DependencyInjection
open MongoDB.Driver


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

let endpoints = [
    subRoute "/admin" [
        GET [
            routef "/search/%s" findPredictions
        ]
    ]
    |> applyBefore Auth.mustBeLoggedIn
    |> applyBefore Auth.mustBeAdmin
]

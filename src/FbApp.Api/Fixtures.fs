module FbApp.Api.Fixtures

open FbApp.Api.Repositories
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.Extensions.DependencyInjection
open MongoDB.Driver
open System


let getFixtureStatus (id: Guid) : HttpHandler =
    (fun next ctx -> task {
        let! dto = Fixtures.getFixtureStatus (ctx.RequestServices.GetRequiredService<IMongoDatabase>()) id
        return! Successful.OK dto next ctx
    })


let scope: Endpoint list = [
    GET [
        routef "/%O/status" getFixtureStatus
    ]
]

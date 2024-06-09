module FbApp.Modules.WebApp.Module

open FbApp.Shared
open Oxpecker
open Oxpecker.Htmx
open Microsoft.AspNetCore.Builder

let configureServices (_: WebApplicationBuilder) =
    ()

let endpoints = [
    yield! Home.endpoints
    yield! Changelog.endpoints
    yield! NotFound.endpoints
    yield! Dashboard.endpoints
]

let fallbackHandler : EndpointHandler =
    fun ctx ->
        match ctx.TryGetHeaderValue HxHeader.Request.Request, ctx.TryGetHeaderValue HxHeader.Request.Boosted with
        | Some _, Some _ -> (setHttpHeader HxHeader.Response.Location Routes.Site.NotFound >=> text "") ctx
        | Some _, None -> (setStatusCode 404 >=> text "") ctx
        | _ -> redirectTo Routes.Site.NotFound false ctx

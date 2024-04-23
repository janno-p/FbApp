module FbApp.Modules.WebApp.Module

open System.Security.Claims
open Oxpecker
open Oxpecker.Htmx
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

open Layouts
open Types


type HttpContext with
    member this.GetModuleLogger(categoryName) =
        this.GetLogger($"FbApp.Modules.WebApp.%s{categoryName}")


let configureServices (_: WebApplicationBuilder) =
    ()


let private getSession (ctx: HttpContext) =
    if ctx.User.Identity.IsAuthenticated then
        let user: UserModel = {
            Name = ctx.User.FindFirstValue(ClaimTypes.Name)
            Picture = ctx.User.FindFirstValue("picture") |> Option.ofObj |> Option.defaultValue "smiley-cyrus.png"
            HasAdminRole = ctx.User.HasClaim(ClaimTypes.Role, "admin")
        }
        Authenticated user
    else Guest


let private defaultView (view: View) =
    fun ctx -> htmlView (defaultLayout None (getSession ctx) view) ctx


let private errorView (view: View) =
    htmlView (errorLayout view)


let endpoints = [
    route Routes.Home (defaultView Home.view)
    route Routes.Changelog (defaultView Changelog.view)
    route Routes.NotFound (errorView NotFound.view)
]


let fallbackHandler : EndpointHandler =
    fun ctx ->
        match ctx.TryGetHeaderValue HxHeader.Request.Request with
        | Some _ ->
            ctx.Response.Headers.Add(HxHeader.Response.Location, "/not-found")
            text "" ctx
        | None -> redirectTo "/not-found" false ctx

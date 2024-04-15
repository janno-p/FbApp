module FbApp.Modules.WebApp.Module

open Oxpecker
open Oxpecker.ViewEngine
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging

open Layouts


type HttpContext with
    member this.GetModuleLogger(categoryName) =
        this.GetLogger($"FbApp.Modules.WebApp.%s{categoryName}")


let configureServices (_: WebApplicationBuilder) =
    ()


let withDefaultLayout (view: {| Title: string; Content: HtmlElement |}) : EndpointHandler =
    fun ctx ->
        let logger = ctx.GetModuleLogger("Module.withDefaultLayout")

        let user =
            if ctx.User.Identity.IsAuthenticated then
                ctx.User.Claims |> Seq.iter (fun c -> logger.LogWarning("User has claim: [{Type}] => {Value}", c.Type, c.Value))
                let user: UserModel = {
                    Name = "Test"
                    Picture = "Test"
                    HasAdminRole = false
                }
                Some user
            else None

        let page: PageModel = {
            CompetitionName = None
            Title = Some view.Title
            User = user
            Content = view.Content
        }

        htmlView (defaultLayout page) ctx


let endpoints = [
    GET [
        route Routes.Home (withDefaultLayout Home.view)
        route Routes.Changelog (withDefaultLayout Changelog.view)
    ]
]

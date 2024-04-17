module FbApp.Modules.WebApp.Module

open System.Security.Claims
open Oxpecker
open Oxpecker.ViewEngine
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

open Layouts


type HttpContext with
    member this.GetModuleLogger(categoryName) =
        this.GetLogger($"FbApp.Modules.WebApp.%s{categoryName}")


let configureServices (_: WebApplicationBuilder) =
    ()


let withDefaultLayout (view: {| Title: string; Content: HtmlElement |}) : EndpointHandler =
    fun ctx ->
        let session =
            if ctx.User.Identity.IsAuthenticated then
                let user: UserModel = {
                    Name = ctx.User.FindFirstValue(ClaimTypes.Name)
                    Picture = ctx.User.FindFirstValue("picture") |> Option.ofObj |> Option.defaultValue "smiley-cyrus.png"
                    HasAdminRole = ctx.User.HasClaim(ClaimTypes.Role, "admin")
                }
                Authenticated user
            else Guest

        let page: PageModel = {
            CompetitionName = None
            Title = Some view.Title
            Session = session
            Content = view.Content
        }

        htmlView (defaultLayout page) ctx


let endpoints = [
    GET [
        route Routes.Home (withDefaultLayout Home.view)
        route Routes.Changelog (withDefaultLayout Changelog.view)
    ]
]

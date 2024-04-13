module FbApp.Modules.WebApp.Module

open Giraffe
open Giraffe.EndpointRouting
open Giraffe.ViewEngine
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging

open Views


type HttpContext with
    member this.GetModuleLogger(categoryName) =
        this.GetLogger($"FbApp.Modules.WebApp.%s{categoryName}")


let configureServices (_: WebApplicationBuilder) =
    ()


let withDefaultLayout (content: XmlNode list) : HttpHandler =
    fun next ctx ->
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
            PageTitle = Some "Home"
            User = user
        }

        htmlView (defaultLayout page content) next ctx


let endpoints = [
    GET [
        route Routes.Home (withDefaultLayout viewHome)
    ]
]

[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.Home

open FbApp.Shared
open Oxpecker
open Oxpecker.ViewEngine


let page: EndpointHandler =
    pageView {
        Layout = Default
        Title = "Home"
        Content = h1() { "Home!" }
    }

let endpoints: Endpoint list = [
    GET [ route Routes.Site.Home page ]
]

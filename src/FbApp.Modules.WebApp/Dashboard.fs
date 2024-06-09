[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.Dashboard

open FbApp.Shared
open Oxpecker
open Oxpecker.ViewEngine

let page: EndpointHandler =
    pageView {
        Layout = Dashboard
        Title = "Dashboard"
        Content = __ () { "Dashboard" }
    }

let endpoints: Endpoint list = [
    (GET >> AUTH >> ADMIN) [ route Routes.Site.Dashboard page ]
]

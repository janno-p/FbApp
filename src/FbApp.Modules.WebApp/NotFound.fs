[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.NotFound

open FbApp.Shared
open Oxpecker
open Oxpecker.ViewEngine

let page: EndpointHandler =
    pageView {
        Layout = Error
        Title = "Page Not Found"
        Content =
            div(class' = "w-full bg-blue-400 text-white text-center p-4 flex items-center justify-center") {
                div() {
                    div(class' = "text-[30vh]") { "404" }
                    div(class' = "text-xl opacity-40") { "Oops. Nothing here..." }
                    a(class' = "mt-8 bg-white text-blue-300", href = "/") { "Go Home" }
                }
            }
    }

let endpoints: Endpoint list = [
    GET [ route Routes.Site.NotFound page ]
]

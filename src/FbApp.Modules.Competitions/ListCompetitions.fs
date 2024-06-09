module internal FbApp.Modules.Competitions.ListCompetitions

open FbApp.Shared
open Oxpecker
open Oxpecker.Htmx
open Oxpecker.ViewEngine

let page: EndpointHandler =
    pageView {
        Layout = Dashboard
        Title = "Competitions"
        Content = __ () {
            button (type' = "button", class' = "text-white bg-blue-700 hover:bg-blue-800 focus:ring-4 focus:ring-blue-300 font-medium rounded-lg text-sm px-5 py-2.5 me-2 mb-2 dark:bg-blue-600 dark:hover:bg-blue-700 focus:outline-none dark:focus:ring-blue-800", hxGet = "/dashboard/competitions/add", hxPushUrl = "true", hxTarget = "#dashboard-section") {
                "Add Competition"
            }
            table (title = "Competitions") {
                thead () {
                    th () { "Description" }
                }
            }
        }
    }

let endpoints: Endpoint list =
    [
        (GET >> AUTH >> ADMIN) [ route "/dashboard/competitions" page ]
    ]

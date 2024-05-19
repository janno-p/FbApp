[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.Dashboard

open Oxpecker.Htmx
open Oxpecker.ViewEngine

open Types


let view (page: string) : View = {
    Title = "Dashboard"
    Content = __ () {
        div (class' = "flex flex-row") {
            div (class' = "flex-none w-48 flex flex-col p-4") {
                a (hxGet = Routes.Dashboard.competitions, hxReplaceUrl = Routes.Dashboard.page("competitions"), hxTarget = "#dashboard-section", class' = "font-medium text-blue-600 hover:underline cursor-pointer") { "Competitions" }
                a (class' = "font-medium text-blue-600 hover:underline cursor-pointer") { "..." }
            }
            div (id = "dashboard-section", class' = "flex-auto p-4", hxGet = $"%s{Routes.Dashboard.root}/%s{page}", hxTrigger = "load") {
                raw "Loading &hellip;"
            }
        }
    }
}


let viewIndex : HtmlElement =
    __() { "Dashboard" }


let viewCompetitions : HtmlElement =
     __ () {
         button (type' = "button", class' = "text-white bg-blue-700 hover:bg-blue-800 focus:ring-4 focus:ring-blue-300 font-medium rounded-lg text-sm px-5 py-2.5 me-2 mb-2 dark:bg-blue-600 dark:hover:bg-blue-700 focus:outline-none dark:focus:ring-blue-800", hxGet = "/dashboard/competitions/add", hxReplaceUrl = "/dashboard?page=competitions/add", hxTarget = "#dashboard-section") {
             "Add Competition"
         }
         table (title = "Competitions") {
             thead () {
                 th () { "Description" }
             }
         }
     }


let viewAddCompetition =
    div (class' = "flex flex-col") {
        h6 () { "Adding a new competition" }
        a (hxGet = "/dashboard/competitions", hxReplaceUrl = "/dashboard?page=competitions", hxTarget = "#dashboard-section") { "Back to competitions list" }

        div () {
            label (for' = "competition-name", class' = "flex") {
                (span (class' = "iconify")).data("icon", "mdi-sign-text")
                "Name:"
            }
            input (id = "competition-name")
        }

        div () {
            label (for' = "competition-season", class' = "flex") {
                (span (class' = "iconify")).data("icon", "mdi-calendar-text")
                "Season:"
            }
            select (id = "competition-season") {
                option () { "Test" }
            }
        }

        div () {
            label (for' = "competition-source", class' = "flex") {
                (span (class' = "iconify")).data("icon", "mdi-import")
                "Tulemuste sisendvoog:"
            }
            select (id = "competition-source") {
                option () { "Test" }
            }
        }

        div () {
            label (for' = "competition-start", class' = "flex") { "Start date:" }
            input (id = "competition-start", type' = "datetime-local")
        }

        div () {
            button (class' = "flex") {
                (span (class' = "iconify")).data("icon", "mdi-check-outline")
                "Add"
            }
        }
    }

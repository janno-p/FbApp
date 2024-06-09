[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.Dashboard

open System
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


let viewAddCompetition () =
    let seasonOptions = [
        let initial = DateTime.Today.Year
        for year = initial downto initial - 5 do
            yield year
    ]

    div (class' = "flex flex-col prose gap-2") {
        div (class' = "mb-4") {
            h1 (class' = "mb-1.5") { "Adding a new competition" }
            a (hxGet = "/dashboard/competitions", hxReplaceUrl = "/dashboard?page=competitions", hxTarget = "#dashboard-section", class' = "link link-hover link-primary") {
                raw "&laquo; Back to competitions list"
            }
        }

        label (class' = "form-control w-full max-w-xs") {
            div (class' = "label") {
                span (class' = "label-text") { "What is the name of the competition?" }
            }
            div (class' = "input input-bordered flex items-center gap-2") {
                span (class' = "icon-[mdi--sign-text]")
                input (name = "name", type' = "text", class' = "grow", placeholder = "Competition name")
            }
        }

        label (class' = "form-control w-full max-w-xs") {
            div (class' = "label") {
                span (class' = "label-text") { "What is the season (!?) of the competition?" }
            }
            div (class' = "input input-bordered flex items-center pr-0") {
                span (class' = "icon-[mdi--calendar-text]")
                select (name = "season", class' = "select bg-transparent focus:outline-none focus:border-none grow pl-2") {
                    for season in seasonOptions do
                        option (value = season.ToString(), selected = (season = seasonOptions[0])) { season.ToString() }
                }
            }
        }

        label (class' = "form-control w-full max-w-xs") {
            div (class' = "label") {
                span (class' = "label-text") { "What is the source of competition results?" }
            }
            div (class' = "input input-bordered flex items-center pr-0") {
                span (class' = "icon-[mdi--database-import]")
                select (name = "source", class' = "select bg-transparent focus:outline-none focus:border-none grow pl-2", hxGet = $"/competitions/%d{seasonOptions[0]}/sources", hxTrigger = "load") {
                    option (disabled = true, selected = true) {
                        raw "Select results source &hellip;"
                    }
                    option () { "Star Wars" }
                    option () { "Harry Potter" }
                    option () { "Lord of the Rings" }
                    option () { "Planet of the Apes" }
                    option () { "Star Trek" }
                }
            }
        }

        label (class' = "form-control w-full max-w-xs") {
            div (class' = "label") {
                span (class' = "label-text") { "When does the competition start?" }
            }
            div (class' = "input input-bordered flex items-center gap-2") {
                span (class' = "icon-[mdi--calendar]")
                input (name = "start-date", type' = "datetime-local", class' = "grow", placeholder = "Competition start date")
            }
        }

        div (class' = "mt-4") {
            button (class' = "btn btn-primary") {
                span (class' = "icon-[mdi--check-outline]")
                "Add"
            }
        }
    }

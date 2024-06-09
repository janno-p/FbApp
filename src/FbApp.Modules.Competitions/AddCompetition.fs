module internal FbApp.Modules.Competitions.AddCompetition

open FbApp.Shared
open Oxpecker
open Oxpecker.Htmx
open Oxpecker.ViewEngine
open System

let page: EndpointHandler =
    let seasonOptions = [
        let initial = DateTime.Today.Year
        for year = initial downto initial - 5 do
            yield year
    ]

    let content =
        div (class' = "flex flex-col prose gap-2") {
            div (class' = "mb-4") {
                h1 (class' = "mb-1.5") { "Adding a new competition" }
                a (hxGet = "/dashboard/competitions", hxPushUrl = "true", hxTarget = "#dashboard-section", class' = "link link-hover link-primary") {
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

    pageView {
        Layout = Dashboard
        Title = "Add competition"
        Content = content
    }

let private save: EndpointHandler =
    text ""

let endpoints: Endpoint list =
    [
        (GET >> AUTH >> ADMIN) [ route "/dashboard/competitions/add" page ]
        (POST >> AUTH >> ADMIN) [ route "/dashboard/competitions/add" save ]
    ]

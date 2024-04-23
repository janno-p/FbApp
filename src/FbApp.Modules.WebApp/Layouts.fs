module internal FbApp.Modules.WebApp.Layouts

open Oxpecker.Htmx
open Oxpecker.ViewEngine

open Types


type UserModel = {
    Name: string
    Picture: string
    HasAdminRole: bool
}


type Session =
    | Authenticated of UserModel
    | Guest


type PageModel = {
    Title: string option
    CompetitionName: string option
    Content: HtmlElement
    Session: Session
}


let private viewUser session : HtmlElement =
    match session with
    | Authenticated user ->
        __() {
            div(class' = "px-2 flex flex-row flex-nowrap items-center grow-0") {
                img(alt = "Avatar", class' = "w-10 h-10 rounded-full", src = user.Picture)
                div(class' = "pl-1 font-medium") { user.Name }
            }

            if user.HasAdminRole then
                a(href = Routes.Dashboard, class' = "text-white grow-0 h-8 flex flex-row flex-nowrap gap-1 items-center justify-center") {
                    span(class' = "material-symbols-outlined") { "manufacturing" }
                    span(class' = "whitespace-nowrap") { "Ava kontrollpaneel" }
                }

            a(href = Routes.Logout, class' = "text-white grow-0 h-8 flex flex-row flex-nowrap gap-1 items-center justify-center", hxBoost = false) {
                span(class' = "material-symbols-outlined") { "logout" }
                span(class' = "whitespace-nowrap") { "Logi välja" }
            }
        }
    | Guest ->
        a(href = Routes.GoogleLogin, class' = "text-white grow-0 w-8 h-8 flex items-center justify-center", title = "Logi sisse Google kontoga", hxBoost = false) {
            span(class' = "material-symbols-outlined") { "login" }
        }


let private viewSiteToolbar competitionName session =
    nav(class' = "glossy bg-sky-600 flex flex-row flex-nowrap items-center text-white px-4 py-1.5 gap-2", hxBoost = true) {
        a(href = Routes.Home, class' = "text-white grow-0") {
            span(class' = "material-symbols-outlined !text-4xl") { "sports_and_outdoors" }
        }
        a(href = Routes.Home, class' = "flex flex-col grow text-white gap-2") {
            span(class' = "grow-0 text-xl leading-4") { "Ennustusmäng" }
            div(class' = "grow uppercase text-sm leading-4") { competitionName |> Option.defaultValue "" }
        }
        a(href = Routes.Changelog, class' = "text-white grow-0 w-8 h-8 flex items-center justify-center", title = "Versioonide ajalugu") {
            span(class' = "material-symbols-outlined") { "checklist" }
        }
        viewUser session
    }


let private viewFooter =
    footer()


let private viewHtmlHead pageTitle =
    head() {
        meta(charset = "UTF-8")
        link(rel = "icon", type' = "image/svg+xml", href = "/favicon.svg")
        link(rel = "stylesheet", href = "/css/app.css")
        link(rel = "stylesheet", href = "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0,0")
        meta(name = "viewport", content = "width=device-width, initial-scale=1.0")
        script(src = "https://unpkg.com/htmx.org@1.9.12", integrity = "sha384-ujb1lZYygJmzgSwoxRggbCHcjc0rB2XoQrxeTUQyRjrOnlCoYta87iKBWq3EsdM2", crossorigin = "anonymous")
        title() { $"%s{pageTitle} - FbApp" }
    }


let defaultLayout (competitionName: string option) (session: Session) (view: View) =
    html(lang = "en") {
        viewHtmlHead view.Title
        body() {
            noscript() { "This is your fallback content in case JavaScript fails to load." }
            viewSiteToolbar competitionName session
            view.Content
            viewFooter
        }
    }


let errorLayout (view: View) =
    html(lang = "en") {
        viewHtmlHead view.Title
        body(class' = "bg-blue-400") {
            noscript() { "This is your fallback content in case JavaScript fails to load." }
            view.Content
        }
    }

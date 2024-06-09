namespace FbApp.Shared

open FbApp.Shared
open Microsoft.AspNetCore.Http
open Oxpecker
open Oxpecker.Htmx
open Oxpecker.ViewEngine
open System.Security.Claims

type PageLayout =
    | Default
    | Dashboard
    | Error

type Page = {
    Title: string
    Layout: PageLayout
    Content: HtmlElement
}

type UserModel = {
    Name: string
    Picture: string
    HasAdminRole: bool
}

type Session =
    | Authenticated of UserModel
    | Guest

type RenderPage = Page -> EndpointHandler

[<AutoOpen>]
module HtmxExtensions =
    let isHtmxNonBoosted (ctx: HttpContext) =
        let isHtmx = ctx.Request.Headers.ContainsKey HxHeader.Request.Request
        let isBoosted = ctx.Request.Headers.ContainsKey HxHeader.Request.Boosted
        isHtmx && (not isBoosted)

[<AutoOpen>]
module Layouts =
    let getSession (ctx: HttpContext) =
        if ctx.User.Identity.IsAuthenticated then
            let user: UserModel = {
                Name = ctx.User.FindFirstValue(ClaimTypes.Name)
                Picture = ctx.User.FindFirstValue("picture") |> Option.ofObj |> Option.defaultValue "smiley-cyrus.png"
                HasAdminRole = ctx.User.HasClaim(ClaimTypes.Role, "admin")
            }
            Authenticated user
        else Guest

    let private viewHtmlHead (pageTitle: string) =
        head () {
            meta (charset = "UTF-8")
            link (rel = "icon", type' = "image/svg+xml", href = "/favicon.svg")
            link (rel = "stylesheet", href = "/css/app.css")
            meta (name = "viewport", content = "width=device-width, initial-scale=1.0")
            script (src = "/js/htmx.min.js")
            title () { pageTitle }
        }

    let private viewUser session : HtmlElement =
        match session with
        | Authenticated user ->
            __ () {
                div(class' = "px-2 flex flex-row flex-nowrap items-center grow-0") {
                    img(alt = "Avatar", class' = "w-10 h-10 rounded-full", src = user.Picture)
                    div(class' = "pl-1 font-medium") { user.Name }
                }

                if user.HasAdminRole then
                    a(href = Routes.Site.Dashboard, class' = "text-white grow-0 h-8 flex flex-row flex-nowrap gap-1 items-center justify-center") {
                        span (class' = "icon-[mdi--manufacturing] text-2xl")
                        span(class' = "whitespace-nowrap") { "Ava kontrollpaneel" }
                    }

                a(href = Routes.Site.Logout, class' = "text-white grow-0 h-8 flex flex-row flex-nowrap gap-1 items-center justify-center", hxBoost = false) {
                    span (class' = "icon-[mdi--logout] text-2xl")
                    span(class' = "whitespace-nowrap") { "Logi välja" }
                }
            }
        | Guest ->
            a(href = Routes.Site.GoogleLogin, class' = "text-white grow-0 w-8 h-8 flex items-center justify-center", title = "Logi sisse Google kontoga", hxBoost = false) {
                span (class' = "icon-[mdi--login] text-2xl")
            }

    let private viewSiteToolbar competitionName session =
        nav(class' = "glossy bg-sky-600 flex flex-row flex-nowrap items-center text-white px-4 py-1.5 gap-2", hxBoost = true) {
            a(href = Routes.Site.Home, class' = "text-white grow-0") {
                span(class' = "icon-[mdi--crystal-ball] text-4xl")
            }
            a(href = Routes.Site.Home, class' = "flex flex-col grow text-white gap-2") {
                span(class' = "grow-0 text-xl leading-4") { "Ennustusmäng" }
                div(class' = "grow uppercase text-sm leading-4") { competitionName |> Option.defaultValue "" }
            }
            a(href = Routes.Site.Changelog, class' = "text-white grow-0 w-8 h-8 flex items-center justify-center", title = "Versioonide ajalugu") {
                span (class' = "icon-[mdi--format-list-checks] text-2xl")
            }
            viewUser session
        }

    let private viewFooter =
        footer()

    let internal defaultPage: RenderPage =
        fun fragment ctx ->
            let competitionName = None
            let session = getSession ctx
            let view =
                html (lang = "en") {
                    viewHtmlHead fragment.Title
                    body() {
                        viewSiteToolbar competitionName session
                        fragment.Content
                        viewFooter
                    }
                }
            htmlView view ctx

    let internal dashboardPage: RenderPage =
        fun fragment ->
            let fragment = {
                fragment with
                    Content =
                        div (class' = "flex flex-row") {
                            div (class' = "flex-none w-48 flex flex-col p-4") {
                                a (hxGet = Routes.Dashboard.Competitions.list, hxPushUrl = "true", hxTarget = "#dashboard-section", class' = "font-medium text-blue-600 hover:underline cursor-pointer") {
                                    "Competitions"
                                }
                                a (class' = "font-medium text-blue-600 hover:underline cursor-pointer") {
                                    "..."
                                }
                            }
                            div (id = "dashboard-section", class' = "flex-auto p-4") {
                                fragment.Content
                            }
                        }
            }
            defaultPage fragment

    let internal errorPage: RenderPage =
        fun fragment ->
            let view =
                html (lang = "en") {
                    viewHtmlHead fragment.Title
                    body(class' = "bg-blue-400") {
                        fragment.Content
                    }
                }
            htmlView view


[<AutoOpen>]
module OxpeckerExtensions =
    let pageView (page: Page) : EndpointHandler =
        fun ctx ->
            let pageTitle =
                match page.Layout with
                | Default -> page.Title
                | Dashboard -> if page.Title <> "Dashboard" then $"Dashboard: %s{page.Title}" else page.Title
                | Error -> page.Title
                |> sprintf "%s - FbApp"
            if isHtmxNonBoosted ctx then
                ctx |> htmlView (
                    __() {
                        title () { pageTitle }
                        page.Content
                    }
                )
            else
                let handler =
                    match page.Layout with
                    | Default -> defaultPage
                    | Dashboard -> dashboardPage
                    | Error -> errorPage
                handler { page with Title = pageTitle } ctx

    let notLoggedIn: EndpointHandler =
        %TypedResults.Unauthorized()

    let notAdmin: EndpointHandler =
        %TypedResults.Forbid()

    let AUTH = applyBefore <| requiresAuthentication notLoggedIn
    let ADMIN = applyBefore <| requiresRole "admin" notAdmin

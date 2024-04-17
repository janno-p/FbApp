[<RequireQualifiedAccess>]
module internal FbApp.NotFound

open Microsoft.AspNetCore.Http
open Oxpecker
open Oxpecker.Htmx
open Oxpecker.ViewEngine


let private errorLayout (view: {| Title: string; Content: HtmlElement |}) =
    html(lang = "en") {
        head() {
            meta(charset = "UTF-8")
            link(rel = "icon", type' = "image/svg+xml", href = "/favicon.svg")
            link(rel = "stylesheet", href = "/css/app.css")
            link(rel = "stylesheet", href = "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0,0")
            meta(name = "viewport", content = "width=device-width, initial-scale=1.0")
            title() { $"%s{view.Title} - FbApp" }
        }
        body(class' = "bg-blue-400") {
            noscript() { "This is your fallback content in case JavaScript fails to load." }
            view.Content
        }
    }


let private view = {|
    Title = "Page Not Found"
    Content =
         // class="flex text-center text-white fullscreen bg-blue q-pa-md flex-center"
         div(class' = "w-full bg-blue-400 text-white text-center p-4 flex items-center justify-center") {
             div() {
                 // style="font-size: 30vh"
                 div(style = "font-size: 30vh") { "404" }

                 // class="text-h2" style="opacity:.4"
                 div(class' = "text-xl", style = "opacity: .4") { "Oops. Nothing here..." }

                 // class="q-mt-xl" color="white" text-color="blue" unelevated to="/" label="Go Home" no-caps
                 a(class' = "mt-8 bg-white text-blue-300", href = "/") { "Go Home" }
             }
         }
|}


let notFoundHandler : EndpointHandler =
    fun (ctx: HttpContext) ->
        let html =
            match ctx.TryGetHeaderValue HxHeader.Request.Request with
            | Some _ ->
                __() {
                    title(hxSwapOob = "outerHTML:title") { $"%s{view.Title} - FbApp" }
                    body(class' = "bg-blue-400", hxSwapOob = "outerHTML:body") { view.Content }
                }
            | None -> errorLayout view
        (setStatusCode 404 >=> htmlView html) ctx

namespace FbApp.Server

open Microsoft.Extensions.DependencyInjection

module XsrfToken =
    open Microsoft.AspNetCore.Antiforgery
    open Microsoft.AspNetCore.Http

    let [<Literal>] CookieName = "XSRF-TOKEN"

    let create (context: HttpContext) =
        let antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>()
        let tokens = antiforgery.GetAndStoreTokens(context)
        tokens.RequestToken

    let refresh (context: HttpContext) =
        context |> create |> ignore

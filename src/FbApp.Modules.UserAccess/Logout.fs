module internal FbApp.Modules.UserAccess.Logout

open FbApp.Modules.UserAccess.Persistence
open FbApp.Shared
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Identity
open Oxpecker

let handler: EndpointHandler =
    fun ctx -> task {
        let signInManager = ctx.GetService<SignInManager<ApplicationUser>>()

        let returnUrl =
            match ctx.Request.Query.TryGetValue("returnUrl") with
            | true, value -> value.ToString()
            | _ -> "/"

        do! signInManager.SignOutAsync()
        do! ctx.SignOutAsync(AuthenticationProperties(RedirectUri = returnUrl))

        return Some ctx
    }

let endpoints: Endpoint list = [
    GET [ route Routes.UserAccess.Logout handler ]
]

module FbApp.Modules.UserAccess.GoogleLogin

open FbApp.Modules.UserAccess.Common
open FbApp.Modules.UserAccess.Persistence
open FbApp.Shared
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Google
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Identity
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Oxpecker
open System
open System.Security.Claims

let configureGoogleOptions (builder: WebApplicationBuilder) (options: GoogleOptions) =
    options.CallbackPath <- PathString Routes.UserAccess.GoogleCallback
    options.SignInScheme <- IdentityConstants.ExternalScheme
    options.Scope.Add("profile")
    options.ClaimActions.MapJsonKey(ClaimTypes.Picture, "picture")
    builder.Configuration.Bind("Modules:UserAccess:Google:Authentication", options)
    options.Events.OnRedirectToAuthorizationEndpoint <- (fun context ->
        context.RedirectUri <- $"%s{context.RedirectUri}&prompt=select_account"
        context.Response.Redirect(context.RedirectUri)
        System.Threading.Tasks.Task.CompletedTask
        )

let private updateUser (user: ApplicationUser) (principal: ClaimsPrincipal) =
    user.Email <- principal.FindFirstValue(ClaimTypes.Email)
    user.EmailConfirmed <- true
    user.FullName <- principal.FindFirstValue(ClaimTypes.Name)
    user.GivenName <- principal.FindFirstValue(ClaimTypes.GivenName)
    user.PictureUrl <- principal.FindFirstValue(ClaimTypes.Picture)
    user.Provider <- "Google"
    user.ProviderId <- principal.FindFirstValue(ClaimTypes.NameIdentifier)
    user.Surname <- principal.FindFirstValue(ClaimTypes.Surname)
    user.UserName <- principal.FindFirstValue(ClaimTypes.Email)

let private updateAdminRole (user: ApplicationUser) (principal: ClaimsPrincipal) (ctx: HttpContext) = task {
    let configuration = ctx.GetService<IConfiguration>()
    let userManager = ctx.GetService<UserManager<ApplicationUser>>()
    let userEmail = principal.FindFirstValue(ClaimTypes.Email)
    let isDefaultAdmin = userEmail.Equals(configuration["Modules:UserAccess:Authorization:DefaultAdmin"])
    let! hasAdminRole = userManager.IsInRoleAsync(user, "admin")
    if isDefaultAdmin <> hasAdminRole then
        let action = if hasAdminRole then userManager.RemoveFromRoleAsync else userManager.AddToRoleAsync
        let! _ = action(user, "admin")
        ()
}

let beginLogin: EndpointHandler =
    fun ctx -> task {
        let logger = ctx.GetLogger("FbApp.Modules.UserAccess.Authentication.googleLogin")
        let signInManager = ctx.GetService<SignInManager<ApplicationUser>>()
        let returnUrl =
            match ctx.Request.Query.TryGetValue("returnUrl") with
            | true, value -> Some (value.ToString())
            | _ -> None
        let redirectUrl =
            let uri =
                UriBuilder(ctx.Request.Scheme, ctx.Request.Host.Host, Path = Routes.UserAccess.GoogleComplete)
            if ctx.Request.Host.Port.HasValue then
                uri.Port <- ctx.Request.Host.Port.Value
            returnUrl
                |> Option.iter (fun ret -> uri.Query <- QueryString.Empty.Add("returnUrl", ret).ToUriComponent())
            uri.Uri.AbsoluteUri
        logger.LogWarning("{Scheme} => {Host} => {X}", ctx.Request.Scheme, ctx.Request.Host, redirectUrl)
        let properties = signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl)
        do! ctx.ChallengeAsync(GoogleDefaults.AuthenticationScheme, properties)
        return Some ctx
    }

let completeCallback: EndpointHandler =
    fun ctx -> task {
        let signInManager = ctx.GetService<SignInManager<ApplicationUser>>()
        let userManager = ctx.GetService<UserManager<ApplicationUser>>()

        let returnUrl =
            match ctx.Request.Query.TryGetValue("returnUrl") with
            | true, value -> value.ToString()
            | _ -> "/"

        match! signInManager.GetExternalLoginInfoAsync() with
        | null ->
            return! beginLogin ctx
        | info ->
            let! result = signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false)
            if result.Succeeded then
                let! user = userManager.FindByNameAsync(info.Principal.FindFirstValue(ClaimTypes.Email))
                updateUser user info.Principal
                do! ctx |> updateAdminRole user info.Principal
                let! _ = userManager.UpdateAsync(user)
                ()
            else
                let user = ApplicationUser()
                updateUser user info.Principal

                let! identityResult = userManager.CreateAsync(user)
                if identityResult.Succeeded then
                    let! _ = userManager.UpdateAsync(user)
                    do! ctx |> updateAdminRole user info.Principal
                    let! identityResult = userManager.AddLoginAsync(user, info)
                    if identityResult.Succeeded then
                        let! _ = signInManager.SignInAsync(user, false)
                        ()

            return! redirectTo returnUrl false ctx
    }

let endpoints: Endpoint list = [
    GET [
        route Routes.UserAccess.GoogleLogin beginLogin
        route Routes.UserAccess.GoogleComplete completeCallback
    ]
]

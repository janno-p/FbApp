module FbApp.Server.Auth

open System.Net.Http
open System.Security.Claims
open FbApp.Server.Configuration
open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Antiforgery
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Options
open Newtonsoft.Json
open Saturn

let [<Literal>] AdministratorRole = "Administrator"

[<AllowNullLiteral>]
type User (email: string, name: string, picture: string, roles: string array, xsrfToken: string) =
    member val Email = email with get
    member val Name = name with get
    member val Picture = picture with get
    member val Roles = roles with get
    member val XsrfToken = xsrfToken with get

[<CLIMutable>]
type Message =
    {
        IdToken: string
    }

[<CLIMutable>]
type TokenInfo =
    {
        Iss: string
        Sub: string
        Azp: string
        Aud: string
        Iat: string
        Exp: string
        Email: string
        EmailVerified: string
        Name: string
        Picture: string
        GivenName: string
        FamilyName: string
        Locale: string
    }

let createUser (claimsPrincipal: ClaimsPrincipal) (context: HttpContext) =
    let email = claimsPrincipal.FindFirst(ClaimTypes.Email).Value
    let name = claimsPrincipal.FindFirst(ClaimTypes.Name).Value
    let picture = claimsPrincipal.FindFirst("Picture").Value
    let roles = claimsPrincipal.FindAll(ClaimTypes.Role) |> Seq.map (fun x -> x.Value) |> Seq.toArray
    let xsrfToken = XsrfToken.create context
    User(email, name, picture, roles, xsrfToken)

let notLoggedIn =
    RequestErrors.FORBIDDEN "You must be logged in"

let resetXsrfToken: HttpHandler =
    (fun next context ->
        task {
            XsrfToken.refresh context
            return! next context
        })

let validateXsrfToken: HttpHandler =
    (fun next context ->
        task {
            let antiforgery = context.GetService<IAntiforgery>()
            try
                do! antiforgery.ValidateRequestAsync(context)
                return! next context
            with e ->
                return! RequestErrors.FORBIDDEN e.Message next context
        })

let tokenSignIn: HttpHandler =
    (fun (next: HttpFunc) (context: HttpContext) ->
        task {
            let authOptions = context.GetService<IOptions<AuthOptions>>().Value
            let googleOptions = context.GetService<IOptions<GoogleOptions>>().Value
            let! message = context.BindJsonAsync<Message>()
            use client = new HttpClient()
            let! tokenInfoString = client.GetStringAsync(sprintf "https://www.googleapis.com/oauth2/v3/tokeninfo?id_token=%s" message.IdToken)
            let tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(tokenInfoString)
            if tokenInfo.Aud = googleOptions.ClientId then
                let claims = [
                    yield Claim(ClaimTypes.Name, tokenInfo.Name)
                    yield Claim(ClaimTypes.Email, tokenInfo.Email)
                    yield Claim("Picture", tokenInfo.Picture)
                    if authOptions.AdminEmails |> Array.contains tokenInfo.Email then
                        yield Claim(ClaimTypes.Role, AdministratorRole)
                ]
                let claimsIdentity = ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
                let claimsPrincipal = ClaimsPrincipal(claimsIdentity)
                do! context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal)
                context.User <- claimsPrincipal
                let user = createUser claimsPrincipal context
                return! Successful.OK user next context
            else
                return! RequestErrors.BAD_REQUEST "Token validation failed!" next context
        })

let getUser (ctx: HttpContext) =
    if ctx.User.Identity.IsAuthenticated then createUser ctx.User ctx else null

let tokenSignOut: HttpHandler =
    signOut CookieAuthenticationDefaults.AuthenticationScheme >=> Successful.OK ()

let authScope = router {
    post "/signin" tokenSignIn
    post "/signout" (resetXsrfToken >=> tokenSignOut)
}

let authPipe = pipeline {
    requires_authentication notLoggedIn
}

let adminPipe: HttpHandler =
    (fun next ctx ->
        task {
            let user = createUser ctx.User ctx
            if user.Roles |> Array.exists ((=) AdministratorRole) then return! next ctx else
            return! RequestErrors.FORBIDDEN (sprintf "This action requires '%s' role." AdministratorRole) next ctx
        })

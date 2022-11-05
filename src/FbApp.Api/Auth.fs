module FbApp.Api.Auth

open FbApp.Api.Configuration
open Giraffe
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Options
open Newtonsoft.Json
open Saturn
open Saturn.Endpoint
open System.Net.Http
open System.Security.Claims

let [<Literal>] AdministratorRole = "Administrator"

[<AllowNullLiteral>]
type User (email: string, name: string, picture: string, roles: string array) =
    member val Email = email with get
    member val Name = name with get
    member val Picture = picture with get
    member val Roles = roles with get

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

let createUser (claimsPrincipal: ClaimsPrincipal) =
    let email = claimsPrincipal.FindFirst(ClaimTypes.Email).Value
    let name = claimsPrincipal.FindFirst(ClaimTypes.Name).Value
    let picture = claimsPrincipal.FindFirst("Picture").Value
    let roles = claimsPrincipal.FindAll(ClaimTypes.Role) |> Seq.map (fun x -> x.Value) |> Seq.toArray
    User(email, name, picture, roles)

let notLoggedIn =
    RequestErrors.FORBIDDEN "You must be logged in"

let tokenSignIn: HttpHandler =
    (fun (next: HttpFunc) (context: HttpContext) ->
        task {
            let authOptions = context.GetService<IOptions<AuthOptions>>().Value
            let googleOptions = context.GetService<IOptions<GoogleOptions>>().Value
            let! message = context.BindJsonAsync<Message>()
            use client = new HttpClient()
            let! tokenInfoString = client.GetStringAsync $"https://www.googleapis.com/oauth2/v3/tokeninfo?id_token=%s{message.IdToken}"
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
                let user = createUser claimsPrincipal
                return! Successful.OK user next context
            else
                return! RequestErrors.BAD_REQUEST "Token validation failed!" next context
        })

let getUser (ctx: HttpContext) =
    if ctx.User.Identity.IsAuthenticated then createUser ctx.User else null

let tokenSignOut: HttpHandler =
    signOut CookieAuthenticationDefaults.AuthenticationScheme >=> Successful.OK ()

let authScope = router {
    post "/signin" tokenSignIn
    post "/signout" tokenSignOut
}

let authPipe = pipeline {
    requires_authentication notLoggedIn
}

let adminPipe: HttpHandler =
    (fun next ctx ->
        task {
            let user = createUser ctx.User
            if user.Roles |> Array.exists ((=) AdministratorRole) then return! next ctx else
            return! RequestErrors.FORBIDDEN $"This action requires '%s{AdministratorRole}' role." next ctx
        })

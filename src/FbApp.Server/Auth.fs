module FbApp.Server.Auth

open System.Net.Http
open System.Security.Claims
open FbApp.Server.Configuration
open FSharp.Control.Tasks.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Antiforgery
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Options
open Newtonsoft.Json
open Saturn

type User =
    {
        Email: string
        Name: string
        Picture: string
        Roles: string[]
        XsrfToken: string
    }

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
    {
        Email = claimsPrincipal.FindFirst(ClaimTypes.Email).Value
        Name = claimsPrincipal.FindFirst(ClaimTypes.Name).Value
        Picture = claimsPrincipal.FindFirst("Picture").Value
        Roles = claimsPrincipal.FindAll(ClaimTypes.Role) |> Seq.map (fun x -> x.Value) |> Seq.toArray
        XsrfToken = XsrfToken.create context
    }

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

let tokenInfo: HttpHandler =
    (fun (next: HttpFunc) (context: HttpContext) ->
        task {
            let handler =
                if context.User.Identity.IsAuthenticated then
                    Successful.OK (createUser context.User context)
                else Successful.OK None
            return! handler next context
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
                        yield Claim(ClaimTypes.Role, "Administrator")
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

let tokenSignOut: HttpHandler =
    signOut CookieAuthenticationDefaults.AuthenticationScheme >=> Successful.OK ()

let authScope = scope {
    post "/info" tokenInfo
    post "/signin" tokenSignIn
    post "/signout" (resetXsrfToken >=> tokenSignOut)
}

let authPipe = pipeline {
    requires_authentication notLoggedIn
}

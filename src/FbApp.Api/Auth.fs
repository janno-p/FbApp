module FbApp.Api.Auth

open Giraffe
open Microsoft.AspNetCore.Authentication.JwtBearer
open Saturn
open System.Security.Claims

let [<Literal>] AdminRole = "admin"

let notLoggedIn =
    RequestErrors.UNAUTHORIZED
        JwtBearerDefaults.AuthenticationScheme
        "FbApp"
        "User is not logged in"

let notAdmin =
    RequestErrors.FORBIDDEN
        "Access denied: user has not sufficient permissions"

let authPipe = pipeline {
    requires_authentication notLoggedIn
}

let adminPipe = pipeline {
    requires_role AdminRole notAdmin
}

type AuthUser = {
    Name: string
    Email: string
}

type AuthHttpHandler = AuthUser -> HttpHandler

let withUser (handler: AuthHttpHandler) : HttpHandler =
    fun next ctx -> task {
        let user = {
            Name = ctx.User.FindFirstValue("name")
            Email = ctx.User.FindFirstValue("email")
        }
        return! handler user next ctx
    }

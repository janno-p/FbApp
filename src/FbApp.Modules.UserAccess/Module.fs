module FbApp.Modules.UserAccess.Module

open Oxpecker
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open Authentication
open Models

let configureServices (builder: WebApplicationBuilder) =
    builder.Services
        .AddAuthentication()
        .AddGoogle(configureGoogleOptions builder)
    |> ignore

    builder.AddNpgsqlDbContext<UserAccessDbContext>("database")

    builder.Services
        .AddIdentity<ApplicationUser, ApplicationRole>()
        .AddEntityFrameworkStores<UserAccessDbContext>()
    |> ignore

let endpoints = [
    GET [
        route Routes.Google googleLogin
        route Routes.GoogleComplete googleResponse
        route Routes.Logout logout
    ]
]

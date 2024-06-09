module FbApp.Modules.UserAccess.Module

open FbApp.Modules.UserAccess
open FbApp.Modules.UserAccess.Persistence
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

let configureServices (builder: WebApplicationBuilder) =
    builder.Services
        .AddAuthentication()
        .AddGoogle(GoogleLogin.configureGoogleOptions builder)
    |> ignore

    builder.AddNpgsqlDbContext<UserAccessDbContext>("database")

    builder.Services
        .AddIdentity<ApplicationUser, ApplicationRole>()
        .AddEntityFrameworkStores<UserAccessDbContext>()
    |> ignore

let endpoints = [
    yield! GoogleLogin.endpoints
    yield! Logout.endpoints
]

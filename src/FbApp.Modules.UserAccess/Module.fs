﻿module FbApp.Modules.UserAccess.Module

open Giraffe.EndpointRouting
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

    builder.AddNpgsqlDbContext<UserAccessDbContext>("fbapp-user-access")

    builder.Services
        .AddIdentity<ApplicationUser, ApplicationRole>()
        .AddEntityFrameworkStores<UserAccessDbContext>()
    |> ignore

    builder.Services
        .AddHostedService<UserAccessDbInitializer>()
    |> ignore


let endpoints = [
    GET [
        route Routes.Google googleLogin
        route Routes.GoogleComplete googleResponse
        route Routes.Logout logout
    ]
]
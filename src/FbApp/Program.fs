module FbApp.Program

open System
open System.Text.Json
open System.Text.Json.Serialization
open Oxpecker
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open FbApp.Modules


let configureAppConfiguration (builder: WebApplicationBuilder) =
    builder.Configuration
        .AddJsonFile("appsettings.user.json", optional=true, reloadOnChange=true)
        .AddEnvironmentVariables()
    |> ignore


let useAuthentication (app: WebApplication) =
    app.UseAuthentication() |> ignore


let useAuthorization (middlewareRequirements: ModuleRequirement list) (app: WebApplication) =
    if middlewareRequirements |> List.exists ((=) RequiresAuthorization) then
        app.UseAuthorization() |> ignore


let useCloudEvents (app: WebApplication) =
    app.UseCloudEvents() |> ignore


let useExceptionHandler (app: WebApplication) =
    app.UseExceptionHandler() |> ignore


let useForwardedHeaders (middlewareRequirements: ModuleRequirement list) (app: WebApplication) =
    middlewareRequirements
    |> List.fold (fun acc x ->
        match x with
        | RequiresForwardedHeaders hdrs ->
            match acc with
            | Some vals -> Some (vals ||| hdrs)
            | None -> Some hdrs
        | _ -> acc
        ) None
    |> Option.iter (fun hdrs ->
        app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = hdrs)) |> ignore
        )


let useOxpecker (enabledModules: ApplicationModule list) (app: WebApplication) =
    let endpoints = enabledModules |> List.map _.Endpoints |> List.concat
    app.UseOxpecker(endpoints) |> ignore
    app.Run(NotFound.notFoundHandler)


let useResponseCompression (app: WebApplication) =
    app.UseResponseCompression() |> ignore


let useRouting (app: WebApplication) =
    app.UseRouting() |> ignore


let useSession (app: WebApplication) =
    app.UseSession() |> ignore


let useStaticFiles (app: WebApplication) =
    app.UseStaticFiles() |> ignore


let configureApp (enabledModules: ApplicationModule list) (app: WebApplication) =
    let moduleRequirements = enabledModules |> List.map _.Requirements |> List.concat

    app |> useForwardedHeaders moduleRequirements
    app |> useExceptionHandler
    app |> useSession
    app |> useResponseCompression
    app |> useStaticFiles
    app |> useRouting
    app |> useCloudEvents
    app |> useAuthentication
    app |> useAuthorization moduleRequirements
    app |> useOxpecker enabledModules

    app.MapDefaultEndpoints() |> ignore
    app.MapSubscribeHandler() |> ignore


let configureAuthentication (builder: WebApplicationBuilder) =
    builder.Services
        .AddAuthentication(fun options ->
            options.DefaultScheme <- CookieAuthenticationDefaults.AuthenticationScheme
            options.DefaultChallengeScheme <- CookieAuthenticationDefaults.AuthenticationScheme
            )
    |> ignore


let configureJsonSerializer (builder: WebApplicationBuilder) =
    let createJsonSerializer (_: IServiceProvider) : Serializers.IJsonSerializer =
        let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
        options.Converters.Add(JsonFSharpConverter())
        SystemTextJson.Serializer options

    builder.Services
        .AddSingleton<Serializers.IJsonSerializer>(createJsonSerializer)
    |> ignore


let configureResponseCompression (builder: WebApplicationBuilder) =
    builder.Services
        .Configure(fun (opts: GzipCompressionProviderOptions) -> opts.Level <- System.IO.Compression.CompressionLevel.Optimal)
        .AddResponseCompression(fun opts ->
            opts.MimeTypes <- Seq.append ResponseCompressionDefaults.MimeTypes [
                "application/x-yaml";
                "image/svg+xml";
                "application/octet-stream";
                "application/x-font-ttf";
                "application/x-font-opentype";
                "application/x-javascript";
                "text/javascript";
            ])
    |> ignore


let configureServices (enabledModules: ApplicationModule list) (builder: WebApplicationBuilder) =
    configureAuthentication builder
    configureJsonSerializer builder
    configureResponseCompression builder

    builder.Services.AddDaprClient()

    builder.Services
        .AddDistributedMemoryCache()
        .AddOxpecker()
        .AddProblemDetails()
        .AddRouting()
        .AddSession()
    |> ignore

    builder.AddServiceDefaults() |> ignore

    enabledModules |> List.iter _.ConfigureServices(builder)


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let enabledModules = getEnabledModules builder.Configuration

    configureAppConfiguration builder
    configureServices enabledModules builder

    let app = builder.Build()
    configureApp enabledModules app

    app.Run()

    0

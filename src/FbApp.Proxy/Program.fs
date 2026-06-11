module FbApp.Proxy.Program


open System.Net.Mime
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open System
open System.Collections.Generic
open System.Threading.Tasks
open Yarp.ReverseProxy.Configuration
open Microsoft.AspNetCore.HttpOverrides
open System.Net


type ProxyJwtBearerEvents () =
    inherit JwtBearerEvents()
    override _.Challenge(context) = task {
        context.HandleResponse()

        if context.AuthenticateFailure = null then () else

        let logger = context.HttpContext.GetService<ILogger<ProxyJwtBearerEvents>>()
        logger.LogWarning("Authentication failed: {Failure}", context.AuthenticateFailure)

        context.Response.StatusCode <- StatusCodes.Status401Unauthorized

        let details = ProblemDetails(
            Status = context.Response.StatusCode,
            Title = "Unauthorized",
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
        )

        let jsonOptions = context.HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value

        do! context.Response.WriteAsJsonAsync(
            details,
            jsonOptions.JsonSerializerOptions,
            MediaTypeNames.Application.Json,
            context.HttpContext.RequestAborted
        )
    }


let configureJwtAuthentication (configuration: IConfiguration) (options: JwtBearerOptions) =
    options.Authority <- configuration.GetConnectionString "authCluster"
    options.TokenValidationParameters.ValidateAudience <- false
    options.TokenValidationParameters.ValidIssuer <- configuration["Authentication:ValidIssuer"]
    options.RequireHttpsMetadata <- false
    options.SaveToken <- true
    options.Events <- ProxyJwtBearerEvents()
    options.Validate()


let configureServices (builder: WebApplicationBuilder) =
    let configuration = builder.Configuration

    builder.AddServiceDefaults() |> ignore

    builder.Services.AddAuthorization()
    |> ignore

    builder.Services.AddGiraffe()
    |> ignore

    builder.Services.AddReverseProxy()
        .LoadFromConfig(configuration.GetSection "ReverseProxy")
    |> ignore

    builder.Services.AddAuthentication(fun cfg ->
        cfg.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
        cfg.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(configureJwtAuthentication configuration)
    |> ignore


let routes = []


let configureApplication (app: WebApplication) =
    let forwardedHeadersOptions = ForwardedHeadersOptions()
    forwardedHeadersOptions.ForwardedHeaders <- ForwardedHeaders.XForwardedHost ||| ForwardedHeaders.XForwardedProto ||| ForwardedHeaders.XForwardedFor

    app.Configuration.GetSection("ForwardedHeaders:AllowedHosts").Get<string[]>()
    |> Option.ofObj
    |> Option.defaultValue [||]
    |> Array.iter (fun host -> forwardedHeadersOptions.AllowedHosts.Add host)

    app.Configuration.GetSection("ForwardedHeaders:KnownIPNetworks").Get<string[]>()
    |> Option.ofObj
    |> Option.defaultValue [||]
    |> Array.iter (fun cidr -> forwardedHeadersOptions.KnownIPNetworks.Add(IPNetwork.Parse cidr))

    app.UseForwardedHeaders forwardedHeadersOptions |> ignore

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseRouting() |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore

    app.MapDefaultEndpoints() |> ignore

    app.UseGiraffe routes |> ignore

    app.UseEndpoints(fun endpoints ->
        endpoints.MapReverseProxy() |> ignore
    ) |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder args
    configureServices builder

    let app = builder.Build()
    configureApplication app

    app.Run()

    0

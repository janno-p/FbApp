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


type DaprConfigFilter(configuration: IConfiguration) =
    let [<Literal>] DaprPrefix = "dapr:"

    let getClusterAddress (clusterId: string) =
        configuration.GetConnectionString(clusterId)
        |> Option.ofObj

    interface IProxyConfigFilter with
        member _.ConfigureClusterAsync(originalCluster, _) =
            let newDestinations = Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)

            match getClusterAddress originalCluster.ClusterId with
            | Some(connectionString) ->
                originalCluster.Destinations
                |> Seq.tryExactlyOne
                |> Option.iter (fun kvp ->
                    let destination = DestinationConfig(
                        Address = connectionString,
                        Health = kvp.Value.Health,
                        Metadata = kvp.Value.Metadata
                    )
                    newDestinations.Add(kvp.Key, destination)
                )
            | None ->
                let daprPort =
                    match Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") |> Int32.TryParse with
                    | true, port -> port
                    | false, _ -> 3500

                originalCluster.Destinations
                |> Seq.iter (fun kvp ->
                    if kvp.Value.Address.StartsWith(DaprPrefix) then
                        let destination = DestinationConfig(
                            Address = $"http://localhost:%d{daprPort}%s{kvp.Value.Address.Substring(DaprPrefix.Length)}",
                            Health = kvp.Value.Health,
                            Metadata = kvp.Value.Metadata
                        )
                        newDestinations.Add(kvp.Key, destination)
                    else
                        newDestinations.Add(kvp.Key, kvp.Value)
                )

            let cluster = ClusterConfig(
                Destinations = newDestinations,
                Metadata = originalCluster.Metadata,
                ClusterId = originalCluster.ClusterId,
                HealthCheck = originalCluster.HealthCheck,
                HttpClient = originalCluster.HttpClient,
                HttpRequest = originalCluster.HttpRequest,
                SessionAffinity = originalCluster.SessionAffinity,
                LoadBalancingPolicy = originalCluster.LoadBalancingPolicy
            )

            ValueTask<ClusterConfig>(cluster)

        member _.ConfigureRouteAsync(originalRoute, _, _) =
            ValueTask<RouteConfig>(originalRoute)


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
    options.Authority <- configuration.GetConnectionString("authCluster")
    options.TokenValidationParameters.ValidateAudience <- false
    options.TokenValidationParameters.ValidIssuer <- configuration["Authentication:ValidIssuer"]
    options.RequireHttpsMetadata <- false
    options.SaveToken <- true
    options.Events <- ProxyJwtBearerEvents()
    options.Validate()


let configureServices (builder: WebApplicationBuilder) =
    let configuration = builder.Configuration

    builder.Services.AddAuthorization()
    |> ignore

    builder.Services.AddGiraffe()
    |> ignore

    builder.Services.AddReverseProxy()
        .LoadFromConfig(configuration.GetSection("ReverseProxy"))
        .AddConfigFilter<DaprConfigFilter>()
    |> ignore

    builder.Services.AddAuthentication(fun cfg ->
        cfg.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
        cfg.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(configureJwtAuthentication configuration)
    |> ignore


let routes = [
    GET [
        route "/dapr/config" (obj() |> Successful.OK)
    ]
]


let configureApplication (app: WebApplication) =
    app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedHost ||| ForwardedHeaders.XForwardedProto))) |> ignore

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseRouting() |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore

    app.UseGiraffe(routes) |> ignore

    app.UseEndpoints(fun endpoints ->
        endpoints.MapReverseProxy() |> ignore
    ) |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder

    let app = builder.Build()
    configureApplication app

    app.Run()

    0

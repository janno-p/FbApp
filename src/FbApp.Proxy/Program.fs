module FbApp.Proxy.Program


open System.Net.Mime
open Giraffe
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Saturn
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
                    let destination = kvp.Value.``<Clone>$``()
                    destination.Address <- connectionString
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
                        let destination = kvp.Value.``<Clone>$``()
                        destination.Address <- $"http://localhost:%d{daprPort}%s{kvp.Value.Address.Substring(DaprPrefix.Length)}"
                        newDestinations.Add(kvp.Key, destination)
                    else
                        newDestinations.Add(kvp.Key, kvp.Value)
                )

            let cluster = originalCluster.``<Clone>$``()
            cluster.Destinations <- newDestinations

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

        do! context.Response.WriteAsJsonAsync(details, null, contentType = MediaTypeNames.Application.Json)
    }


let configureJwtAuthentication (configuration: IConfiguration) (options: JwtBearerOptions) =
    options.Authority <- configuration.GetConnectionString("authCluster")
    options.TokenValidationParameters.ValidateAudience <- false
    options.TokenValidationParameters.ValidIssuer <- configuration["Authentication:ValidIssuer"]
    options.RequireHttpsMetadata <- false
    options.SaveToken <- true
    options.Events <- ProxyJwtBearerEvents()
    options.Validate()


let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
    let configuration = context.Configuration

    services.AddAuthorization()
    |> ignore

    services.AddReverseProxy()
        .LoadFromConfig(configuration.GetSection("ReverseProxy"))
        .AddConfigFilter<DaprConfigFilter>()
    |> ignore

    services.AddAuthentication(fun cfg ->
        cfg.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
        cfg.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(configureJwtAuthentication configuration)
    |> ignore


let routes = router {
    get "/dapr/config" (obj() |> Successful.OK)
}


let configureApplication (app: IApplicationBuilder) =
    let env = Environment.getWebHostEnvironment app

    app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedHost ||| ForwardedHeaders.XForwardedProto))) |> ignore

    if env.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseRouting() |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore

    app.UseGiraffe(routes)

    app.UseEndpoints(fun endpoints ->
        endpoints.MapReverseProxy() |> ignore
    ) |> ignore

    app


let app = application {
    no_router
    app_config configureApplication
    host_config (fun host -> host.ConfigureServices(configureServices))
}


run app

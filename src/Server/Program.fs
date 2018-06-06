module FbApp.Server.Program

open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server.Common
open FbApp.Server.HttpsConfig
open Giraffe
open Giraffe.HttpStatusCodeHandlers
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Configuration.UserSecrets
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Saturn
open System.IO

let clientPath = Path.Combine("..", "Client", "dist", "spa-mat") |> Path.GetFullPath

let mainRouter = scope {
    get "/" (Path.Combine(clientPath, "index.html") |> ResponseWriters.htmlFile)

    forward "/api/auth" Auth.authScope

    forward "/api" (scope {
        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken

        forward "/dashboard" Dashboard.dashboardScope
    })
}

let endpoints = [
    { EndpointConfiguration.Default with
        Port = Some 5000 }
    { EndpointConfiguration.Default with 
        Port = Some 5001
        Scheme = Https
        FilePath = Some (Path.Combine(__SOURCE_DIRECTORY__, "..", "FbApp.pfx")) }
]

let configureServices (context: WebHostBuilderContext) (services: IServiceCollection) =
    services.AddAntiforgery (fun opt -> opt.HeaderName <- "X-XSRF-TOKEN") |> ignore
    services.Configure<AuthOptions>(context.Configuration.GetSection("Authentication")) |> ignore
    services.Configure<GoogleOptions>(context.Configuration.GetSection("Authentication:Google")) |> ignore
    FootballData.footballDataToken <- context.Configuration.["Authentication:FootballDataToken"]

    Aggregate.Handlers.competitionHandler <-
        Aggregate.makeHandler
            { InitialState = Competition.initialState; Decide = Competition.decide; Evolve = Competition.evolve }
            (EventStore.makeRepository EventStore.connection "Competition" Serialization.serialize Serialization.deserialize)

let configureAppConfiguration (context: WebHostBuilderContext) (config: IConfigurationBuilder) =
    config.AddJsonFile("appsettings.json", optional=false, reloadOnChange=true)
          .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName, optional=true, reloadOnChange=true)
          .AddEnvironmentVariables()
          .AddUserSecrets<EndpointConfiguration>()
    |> ignore

let app = application {
    router mainRouter
    memory_cache
    use_gzip

    use_cookies_authentication "jnx.era.ee"

    app_config (fun app ->
        app.UseStaticFiles(
            new StaticFileOptions(
                FileProvider = new PhysicalFileProvider(clientPath),
                RequestPath = PathString.Empty
            )
        ) |> ignore

        Projection.connectSubscription EventStore.connection
        ProcessManager.connectSubscription EventStore.connection

        app
    )

    host_config (fun host ->
        host.UseKestrel(fun o -> o.ConfigureEndpoints endpoints)
            .ConfigureAppConfiguration(configureAppConfiguration)
            .ConfigureServices(configureServices)
    )
}

run app

[<assembly: UserSecretsIdAttribute("d6072641-6e1a-4bbc-bbb6-d355f0e38db4")>]
do()

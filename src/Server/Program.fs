module FbApp.Server.Program

open FbApp.Server
open FbApp.Server.Common
open FbApp.Server.HttpsConfig
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Configuration.UserSecrets
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Logging
open Saturn
open System.IO
open Microsoft.AspNetCore.HttpOverrides

let mainRouter = scope {
    get "/" (Path.Combine("wwwroot", "index.html") |> ResponseWriters.htmlFile)

    forward "/api/auth" Auth.authScope
    forward "/api/predict" Predict.predictScope

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
            { InitialState = Competition.initialState; Decide = Competition.decide; Evolve = Competition.evolve; StreamId = id }
            (EventStore.makeRepository EventStore.connection "Competition" Serialization.serialize Serialization.deserialize)

    Aggregate.Handlers.predictionHandler <-
            Aggregate.makeHandler
                { InitialState = Prediction.initialState; Decide = Prediction.decide; Evolve = Prediction.evolve; StreamId = Prediction.streamId }
                (EventStore.makeRepository EventStore.connection "Prediction" Serialization.serialize Serialization.deserialize)

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

    app_config (fun app ->
        app.UseForwardedHeaders(new ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)))
        |> ignore

        let env = app.ApplicationServices.GetService<IHostingEnvironment>()

        if env.IsProduction() then
            app.UseStaticFiles(
                new StaticFileOptions(
                    FileProvider = new PhysicalFileProvider(Path.GetFullPath("wwwroot")),
                    RequestPath = PathString.Empty
                ))
            |> ignore

        let loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>()

        Projection.connectSubscription EventStore.connection loggerFactory
        ProcessManager.connectSubscription EventStore.connection loggerFactory

        app
    )

    use_cookies_authentication "jnx.era.ee"

    host_config (fun host ->
        host.UseKestrel(fun o -> o.ConfigureEndpoints endpoints)
            .ConfigureAppConfiguration(configureAppConfiguration)
            .ConfigureServices(configureServices)
    )
}

run app

[<assembly: UserSecretsIdAttribute("d6072641-6e1a-4bbc-bbb6-d355f0e38db4")>]
do()

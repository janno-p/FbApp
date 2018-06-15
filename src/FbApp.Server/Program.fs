module FbApp.Server.Program

open FbApp.Core.Aggregate
open FbApp.Core.EventStore
open FbApp.Domain
open FbApp.Server
open FbApp.Server.Configuration
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
open EventStore.ClientAPI
open Microsoft.Extensions.Options
open System

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

let initializeEventStore (sp: IServiceProvider) = task {
    let options = sp.GetService<IOptions<EventStoreOptions>>().Value
    let! connection = createEventStoreConnection options
    do! EventStore.initProjectionsAndSubscriptions (connection, options)
    return connection
}

let configureServices (context: WebHostBuilderContext) (services: IServiceCollection) =
    services.AddAntiforgery (fun opt -> opt.HeaderName <- "X-XSRF-TOKEN") |> ignore

    services.Configure<AuthOptions>(context.Configuration.GetSection("Authentication")) |> ignore
    services.Configure<GoogleOptions>(context.Configuration.GetSection("Authentication:Google")) |> ignore
    services.Configure<EventStoreOptions>(context.Configuration.GetSection("EventStore")) |> ignore

    services.AddSingleton<IEventStoreConnection>((fun sp -> (initializeEventStore sp).Result)) |> ignore

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

        let authOptions = app.ApplicationServices.GetService<IOptions<AuthOptions>>().Value
        let loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>()
        let eventStoreConnection = app.ApplicationServices.GetService<IEventStoreConnection>()

        Projection.connectSubscription eventStoreConnection loggerFactory
        ProcessManager.connectSubscription eventStoreConnection loggerFactory authOptions

        CommandHandlers.competitionsHandler <-
            makeHandler
                { InitialState = Competitions.initialState; Decide = Competitions.decide; Evolve = Competitions.evolve; StreamId = id }
                (makeDefaultRepository eventStoreConnection "Competition")

        CommandHandlers.predictionsHandler <-
            makeHandler
                { InitialState = Predictions.initialState; Decide = Predictions.decide; Evolve = Predictions.evolve; StreamId = Predictions.streamId }
                (makeDefaultRepository eventStoreConnection "Prediction")

        CommandHandlers.fixturesHandler <-
            makeHandler
                { InitialState = Fixtures.initialState; Decide = Fixtures.decide; Evolve = Fixtures.evolve; StreamId = Fixtures.streamId }
                (makeDefaultRepository eventStoreConnection Fixtures.AggregateName)

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

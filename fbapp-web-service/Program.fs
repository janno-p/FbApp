module FbApp.Web.Program

open FbApp.Core.Aggregate
open FbApp.Core.EventStore
open FbApp.Domain
open FbApp.Web
open FbApp.Web.Configuration
open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Saturn
open System.IO
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.Extensions.Options
open System

[<CLIMutable>]
type AppBootstrapInfo =
    {
        CompetitionStatus: string
        User: Auth.User
    }

let appBootstrap : HttpHandler =
    (fun next ctx -> task {
        let user =
            Auth.getUser ctx
        let! competitionStatus =
            Predict.getCompetitionStatus (ctx.RequestServices.GetRequiredService<IMongoDatabase>())
        let dto =
            {
                CompetitionStatus = competitionStatus
                User = user
            }
        return! Successful.OK dto next ctx
    })

let mainRouter = router {
    not_found_handler (text "Not Found")

    get "/api/bootstrap" appBootstrap

    forward "/api/auth" Auth.authScope
    forward "/api/predict" Predict.routes
    forward "/api/fixtures" Fixtures.routes
    forward "/api/predictions" Predictions.routes
    forward "/api/leagues" Leagues.routes

    forward "/api" (router {
        not_found_handler (RequestErrors.NOT_FOUND "Not found")

        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        forward "/dashboard" Dashboard.routes
    })
}

let initializeMongoDb (sp: IServiceProvider) =
    let configuration = sp.GetService<IConfiguration>()
    let client =
        match configuration.GetConnectionString("mongodb") with
        | null -> MongoClient()
        | value -> MongoClient(value)
    client.GetDatabase("fbapp")

let initializeEventStore (sp: IServiceProvider) =
    let configuration = sp.GetService<IConfiguration>()
    let connectionString =
        configuration.GetConnectionString("eventstore")
        |> Option.ofObj
        |> Option.defaultValue configuration.["EventStore:Uri"]
        |> EventStoreConnectionString
    createEventStoreClient connectionString

let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
    services.AddAntiforgery (fun opt -> opt.HeaderName <- "X-XSRF-TOKEN") |> ignore
    services.Configure<AuthOptions>(context.Configuration.GetSection("Authentication")) |> ignore
    services.Configure<GoogleOptions>(context.Configuration.GetSection("Authentication:Google")) |> ignore
    services.AddSingleton<EventStore.Client.EventStoreClient>(initializeEventStore) |> ignore
    services.AddSingleton<IMongoDatabase>(initializeMongoDb) |> ignore

let configureAppConfiguration (context: HostBuilderContext) (config: IConfigurationBuilder) =
    config.AddJsonFile("appsettings.json", optional=true, reloadOnChange=true)
          .AddJsonFile($"appsettings.%s{context.HostingEnvironment.EnvironmentName}.json", optional=true, reloadOnChange=true)
          .AddJsonFile("appsettings.user.json", optional=true, reloadOnChange=true)
          .AddEnvironmentVariables()
    |> ignore

let app = application {
    use_router mainRouter
    memory_cache
    use_gzip

    app_config (fun app ->
        app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)))
        |> ignore

        let env = Environment.getWebHostEnvironment app

        if env.IsProduction() then
            app.UseStaticFiles(
                StaticFileOptions(
                    FileProvider = new PhysicalFileProvider(Path.GetFullPath("wwwroot")),
                    RequestPath = PathString.Empty
                ))
            |> ignore

        let authOptions = app.ApplicationServices.GetService<IOptions<AuthOptions>>().Value
        let loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>()
        let eventStoreClient = app.ApplicationServices.GetService<EventStore.Client.EventStoreClient>()
        let mongoDb = app.ApplicationServices.GetRequiredService<IMongoDatabase>()

        (Projection.connectSubscription eventStoreClient mongoDb loggerFactory).Wait()
        (ProcessManager.connectSubscription eventStoreClient mongoDb loggerFactory authOptions).Wait()

        CommandHandlers.competitionsHandler <-
            makeHandler { Decide = Competitions.decide; Evolve = Competitions.evolve } (makeDefaultRepository eventStoreClient Competitions.AggregateName)

        CommandHandlers.predictionsHandler <-
            makeHandler { Decide = Predictions.decide; Evolve = Predictions.evolve } (makeDefaultRepository eventStoreClient Predictions.AggregateName)

        CommandHandlers.fixturesHandler <-
            makeHandler { Decide = Fixtures.decide; Evolve = Fixtures.evolve } (makeDefaultRepository eventStoreClient Fixtures.AggregateName)

        CommandHandlers.leaguesHandler <-
            makeHandler { Decide = Leagues.decide; Evolve = Leagues.evolve } (makeDefaultRepository eventStoreClient Leagues.AggregateName)

        app
    )

    use_cookies_authentication "jnx.era.ee"

    host_config (fun host ->
        host.ConfigureAppConfiguration(configureAppConfiguration)
            .ConfigureServices(configureServices)
    )
}

run app

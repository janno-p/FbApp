﻿module FbApp.Server.Program

open FbApp.Core.Aggregate
open FbApp.Core.EventStore
open FbApp.Domain
open FbApp.Server
open FbApp.Server.Configuration
open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Configuration.UserSecrets
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Saturn
open System.IO
open Microsoft.AspNetCore.HttpOverrides
open EventStore.ClientAPI
open Microsoft.Extensions.Options
open System

let index : HttpHandler =
    (Path.Combine("wwwroot", "index.html") |> htmlFile)

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
            Predict.getCompetitionStatus ()
        let dto =
            {
                CompetitionStatus = competitionStatus
                User = user
            }
        return! Successful.OK dto next ctx
    })

let mainRouter = router {
    not_found_handler index
    get "/" index

    get "/api/bootstrap" appBootstrap

    forward "/api/auth" Auth.authScope
    forward "/api/predict" Predict.predictScope
    forward "/api/fixtures" Fixtures.scope
    forward "/api/predictions" Predictions.scope
    forward "/api/leagues" Leagues.scope

    forward "/api" (router {
        not_found_handler (RequestErrors.NOT_FOUND "Not found")

        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        forward "/dashboard" Dashboard.dashboardScope
    })
}

let initializeEventStore (sp: IServiceProvider) = task {
    let options = sp.GetService<IOptions<EventStoreOptions>>().Value
    let! connection = createEventStoreConnection options
    return connection
}

let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
    services.AddAntiforgery (fun opt -> opt.HeaderName <- "X-XSRF-TOKEN") |> ignore

    services.Configure<AuthOptions>(context.Configuration.GetSection("Authentication")) |> ignore
    services.Configure<GoogleOptions>(context.Configuration.GetSection("Authentication:Google")) |> ignore
    services.Configure<EventStoreOptions>(context.Configuration.GetSection("EventStore")) |> ignore
    services.Configure<SubscriptionsSettings>(context.Configuration.GetSection("EventStore:Subscriptions")) |> ignore

    services.AddSingleton<IEventStoreConnection>((fun sp -> (initializeEventStore sp).Result)) |> ignore

let configureAppConfiguration (context: HostBuilderContext) (config: IConfigurationBuilder) =
    config.AddJsonFile("appsettings.json", optional=true, reloadOnChange=true)
          .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName, optional=true, reloadOnChange=true)
          .AddEnvironmentVariables()
    |> ignore

let app = application {
    use_router mainRouter
    memory_cache
    use_gzip

    app_config (fun app ->
        app.UseForwardedHeaders(new ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)))
        |> ignore

        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()

        if env.IsProduction() then
            app.UseStaticFiles(
                new StaticFileOptions(
                    FileProvider = new PhysicalFileProvider(Path.GetFullPath("wwwroot")),
                    RequestPath = PathString.Empty
                ))
            |> ignore

        let authOptions = app.ApplicationServices.GetService<IOptions<AuthOptions>>().Value
        let subscriptionsSettings = app.ApplicationServices.GetRequiredService<IOptions<SubscriptionsSettings>>().Value

        let loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>()
        let eventStoreConnection = app.ApplicationServices.GetService<IEventStoreConnection>()

        Projection.connectSubscription eventStoreConnection loggerFactory subscriptionsSettings
        ProcessManager.connectSubscription eventStoreConnection loggerFactory authOptions subscriptionsSettings

        CommandHandlers.competitionsHandler <-
            makeHandler { Decide = Competitions.decide; Evolve = Competitions.evolve } (makeDefaultRepository eventStoreConnection Competitions.AggregateName)

        CommandHandlers.predictionsHandler <-
            makeHandler { Decide = Predictions.decide; Evolve = Predictions.evolve } (makeDefaultRepository eventStoreConnection Predictions.AggregateName)

        CommandHandlers.fixturesHandler <-
            makeHandler { Decide = Fixtures.decide; Evolve = Fixtures.evolve } (makeDefaultRepository eventStoreConnection Fixtures.AggregateName)

        CommandHandlers.leaguesHandler <-
            makeHandler { Decide = Leagues.decide; Evolve = Leagues.evolve } (makeDefaultRepository eventStoreConnection Leagues.AggregateName)

        app
    )

    use_cookies_authentication "jnx.era.ee"

    host_config (fun host ->
        host.ConfigureAppConfiguration(configureAppConfiguration)
            .ConfigureServices(configureServices)
    )
}

run app

[<assembly: UserSecretsIdAttribute("d6072641-6e1a-4bbc-bbb6-d355f0e38db4")>]
do()

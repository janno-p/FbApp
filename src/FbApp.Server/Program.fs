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
open Saturn.Endpoint
open System.IO
open Microsoft.AspNetCore.HttpOverrides
open EventStore.ClientAPI
open Microsoft.Extensions.Options
open System
open Giraffe.EndpointRouting.Routers
open Dapr
open Newtonsoft.Json
open Newtonsoft.Json.Serialization

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


let mapGoals (value: (int * int) option) : Fixtures.FixtureGoals option =
    value |> Option.map (fun (home, away) -> { Home = home; Away = away })


let updateFixtures: HttpHandler =
    (fun next ctx -> task {
        let logger = ctx.GetLogger()
        let! evt = ctx.BindJsonAsync<FixturesUpdatedIntegrationEvent>()
        for fixture in evt.Fixtures do
            let competitionGuid = Competitions.createId fixture.CompetitionId
            let fixtureId = Fixtures.createId (competitionGuid, fixture.FixtureId)
            let! result =
                match fixture.Stage with
                | "GROUP_STAGE" ->
                    Fixtures.UpdateFixture {
                        Status = fixture.Status
                        FullTime = mapGoals fixture.FullTime
                        ExtraTime = mapGoals fixture.ExtraTime
                        Penalties = mapGoals fixture.Penalties
                        }
                    |> CommandHandlers.fixturesHandler (fixtureId, Any)
                | "ROUND_OF_16" | "QUARTER_FINALS" | "SEMI_FINALS" | "FINAL" ->
                    Fixtures.UpdateQualifiers {
                        CompetitionId = competitionGuid
                        ExternalId = fixture.FixtureId
                        HomeTeamId = fixture.HomeTeamId |> Option.defaultValue 0L
                        AwayTeamId = fixture.AwayTeamId |> Option.defaultValue 0L
                        Date = fixture.UtcDate
                        Stage = fixture.Stage
                        Status = fixture.Status
                        FullTime = mapGoals fixture.FullTime
                        ExtraTime = mapGoals fixture.ExtraTime
                        Penalties = mapGoals fixture.Penalties
                        }
                    |> CommandHandlers.fixturesHandler (fixtureId, Any)
                | _ ->
                    TaskResult.FromResult(Error (DomainError (Fixtures.Error.UnexpectedStage fixture.Stage)))

            match result with
            | Error(err) ->
                logger.LogError($"Failed to update fixture %A{id}: %A{err}")
            | Ok _ ->
                ()

        return! ServerErrors.INTERNAL_ERROR "" next ctx
    })

let mainRouter = router {
    // not_found_handler index
    get "/" index

    get "/api/bootstrap" appBootstrap

    forward "/api/auth" Auth.authScope
    forward "/api/predict" Predict.predictScope
    forward "/api/fixtures" Fixtures.scope
    forward "/api/predictions" Predictions.scope
    forward "/api/leagues" Leagues.scope

    forward "/api" (router {
        // not_found_handler (RequestErrors.NOT_FOUND "Not found")

        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        forward "/dashboard" Dashboard.dashboardScope
    })

    forward "/api/fixture-updates" (POST [
        route "" updateFixtures
        |> addMetadata (TopicAttribute("live-update-pubsub", "fixture-updates"))
    ])
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


let configureApp (app: IApplicationBuilder) =
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

    app.UseRouting() |> ignore
    app.UseCloudEvents() |> ignore

    app.Use(fun ctx next -> unitTask {
        ctx.Request.EnableBuffering()
        let log = ctx.RequestServices.GetRequiredService<ILogger<obj>>()
        log.LogWarning("After cloudenvets")
        use mem = new MemoryStream()
        do! ctx.Request.Body.CopyToAsync(mem)
        mem.Position <- 0L
        ctx.Request.Body.Position <- 0L
        log.LogWarning(System.Text.Encoding.UTF8.GetString(mem.ToArray()))
        return! next.Invoke()
    }) |> ignore

    app.UseEndpoints(fun endpoints ->
        endpoints.MapSubscribeHandler() |> ignore
    ) |> ignore

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


let configureHost (host: IHostBuilder) =
    host.ConfigureAppConfiguration(configureAppConfiguration)
        .ConfigureServices(configureServices)


let jsonSettings = JsonSerializerSettings()
jsonSettings.Converters.Add(OptionConverter())
jsonSettings.ContractResolver <- CamelCasePropertyNamesContractResolver()


let app = application {
    use_endpoint_router mainRouter
    memory_cache
    use_gzip
    app_config configureApp
    use_cookies_authentication "jnx.era.ee"
    host_config configureHost
    use_json_settings jsonSettings
}

run app

[<assembly: UserSecretsIdAttribute("d6072641-6e1a-4bbc-bbb6-d355f0e38db4")>]
do()

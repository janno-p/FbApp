module FbApp.Api.Program


open Dapr
open EventStore.ClientAPI
open FbApp.Api
open FbApp.Api.Aggregate
open FbApp.Api.Configuration
open FbApp.Api.Domain
open FbApp.Api.EventStore
open FSharp.Control.Tasks
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open Saturn
open Saturn.Endpoint
open System
open System.IO


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
            let maybeCommand =
                match fixture.Stage with
                | "GROUP_STAGE" ->
                    Fixtures.UpdateFixture {
                        Status = fixture.Status
                        FullTime = mapGoals fixture.FullTime
                        ExtraTime = mapGoals fixture.ExtraTime
                        Penalties = mapGoals fixture.Penalties
                        }
                    |> Some
                | "LAST_16" | "QUARTER_FINAL" | "SEMI_FINAL" | "FINAL" ->
                    match fixture.HomeTeamId, fixture.AwayTeamId with
                    | Some(homeTeamId), Some(awayTeamId) ->
                        Fixtures.UpdateQualifiers {
                            CompetitionId = competitionGuid
                            ExternalId = fixture.FixtureId
                            HomeTeamId = homeTeamId
                            AwayTeamId = awayTeamId
                            Date = fixture.UtcDate
                            Stage = fixture.Stage
                            Status = fixture.Status
                            FullTime = mapGoals fixture.FullTime
                            ExtraTime = mapGoals fixture.ExtraTime
                            Penalties = mapGoals fixture.Penalties
                            }
                        |> Some
                    | _ ->
                        None
                | _ ->
                    logger.LogError($"Unknown stage value for fixture %A{id}: %s{fixture.Stage}")
                    None

            match maybeCommand with
            | Some command ->
                let fixtureId = Fixtures.createId (competitionGuid, fixture.FixtureId)
                match! CommandHandlers.fixturesHandler (fixtureId, Any) command with
                | Ok _ ->
                    ()
                | Error err ->
                    logger.LogError($"Failed to update fixture %A{id}: %A{err}")
            | None ->
                ()

        return! Successful.OK "" next ctx
    })


let mainRouter = router {
    get "/api/bootstrap" appBootstrap

    forward "/api/auth" Auth.authScope
    forward "/api/predict" Predict.predictScope
    forward "/api/fixtures" Fixtures.scope
    forward "/api/predictions" Predictions.scope
    forward "/api/leagues" Leagues.scope

    forward "/api" (router {
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
    services.AddRouting() |> ignore

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
    app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)))
    |> ignore

    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()

    if env.IsProduction() then
        app.UseStaticFiles(
            StaticFileOptions(
                FileProvider = new PhysicalFileProvider(Path.GetFullPath("wwwroot")),
                RequestPath = PathString.Empty
            ))
        |> ignore

    app.UseRouting() |> ignore
    app.UseCloudEvents() |> ignore

    app.UseEndpoints(fun endpoints ->
        endpoints.MapSubscribeHandler() |> ignore
        endpoints.MapGiraffeEndpoints(mainRouter) |> ignore
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
    no_router
    memory_cache
    use_gzip
    app_config configureApp
    use_cookies_authentication "jnx.era.ee"
    host_config configureHost
    use_json_settings jsonSettings
}


run app

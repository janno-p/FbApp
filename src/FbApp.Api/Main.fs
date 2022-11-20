module FbApp.Api.Main


open System.IdentityModel.Tokens.Jwt
open System.Text.Json
open System.Text.Json.Serialization
open Dapr
open EventStore.Client
open FbApp.Api
open FbApp.Api.Aggregate
open FbApp.Api.Configuration
open FbApp.Api.Domain
open FbApp.Api.EventStore
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.IdentityModel.Tokens
open MongoDB.Driver
open Saturn
open Saturn.Endpoint
open System


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
    get "/dapr/config" (obj() |> Successful.OK)

    get "/api/competition/status" FbApp.Competition.Api.getCompetitionStatus
    get "/api/prediction" (Auth.mustBeLoggedIn >=> (Auth.withUser FbApp.Prediction.Api.getUserPrediction))

    forward "/api/predict" Predict.predictScope
    forward "/api/fixtures" Fixtures.scope
    forward "/api/predictions" Predictions.scope
    forward "/api/leagues" Leagues.scope

    forward "/api" (router {
        pipe_through Auth.mustBeLoggedIn
        pipe_through Auth.mustBeAdmin

        forward "/dashboard" Dashboard.dashboardScope
    })

    forward "/api/fixture-updates" (POST [
        route "" updateFixtures
        |> addMetadata (TopicAttribute("live-update-pubsub", "fixture-updates"))
    ])
}


let initializeMongoDb (sp: IServiceProvider) =
    let configuration = sp.GetService<IConfiguration>()
    let client =
        match configuration.GetConnectionString("mongodb") with
        | null -> MongoClient()
        | value -> MongoClient(value)
    client.GetDatabase("fbapp")


let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
    services.AddRouting() |> ignore

    services.Configure<AuthOptions>(context.Configuration.GetSection("Authentication")) |> ignore
    services.Configure<SubscriptionsSettings>(context.Configuration.GetSection("EventStore:Subscriptions")) |> ignore

    let eventStoreConnection =
        context.Configuration.GetConnectionString("eventstore")
        |> Option.ofObj
        |> Option.defaultValue (context.Configuration.GetValue("EventStore:Uri"))

    let setConnectionName (settings: EventStoreClientSettings) =
        settings.ConnectionName <- "fbapp"

    services.AddEventStoreClient(eventStoreConnection, setConnectionName) |> ignore
    services.AddEventStoreProjectionManagementClient(eventStoreConnection, setConnectionName) |> ignore
    services.AddEventStorePersistentSubscriptionsClient(eventStoreConnection, setConnectionName) |> ignore

    services.AddSingleton<IMongoDatabase>(initializeMongoDb) |> ignore


let configureAppConfiguration (context: HostBuilderContext) (config: IConfigurationBuilder) =
    config.AddJsonFile("appsettings.json", optional=true, reloadOnChange=true)
          .AddJsonFile($"appsettings.%s{context.HostingEnvironment.EnvironmentName}.json", optional=true, reloadOnChange=true)
          .AddJsonFile("appsettings.user.json", optional=true, reloadOnChange=true)
          .AddEnvironmentVariables()
    |> ignore


let configureApp (app: IApplicationBuilder) =
    let forwardedHeaders = ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto

    app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = forwardedHeaders))
        .UseRouting()
        .UseCloudEvents()
        .UseEndpoints(fun endpoints ->
            endpoints.MapSubscribeHandler() |> ignore
            endpoints.MapGiraffeEndpoints(mainRouter)
        )
    |> ignore

    let client = app.ApplicationServices.GetService<EventStoreClient>()

    CommandHandlers.competitionsHandler <-
        makeHandler { Decide = Competitions.decide; Evolve = Competitions.evolve } (makeDefaultRepository client Competitions.AggregateName)

    CommandHandlers.predictionsHandler <-
        makeHandler { Decide = Predictions.decide; Evolve = Predictions.evolve } (makeDefaultRepository client Predictions.AggregateName)

    CommandHandlers.fixturesHandler <-
        makeHandler { Decide = Fixtures.decide; Evolve = Fixtures.evolve } (makeDefaultRepository client Fixtures.AggregateName)

    CommandHandlers.leaguesHandler <-
        makeHandler { Decide = Leagues.decide; Evolve = Leagues.evolve } (makeDefaultRepository client Leagues.AggregateName)

    let processManagerInitTask =
        ProcessManager.connectSubscription app.ApplicationServices

    processManagerInitTask.Wait()

    let initCompetition = task {
        use scope = app.ApplicationServices.CreateScope()
        let db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>()

        let! count =
            db.GetCollection<MongoDB.Bson.BsonDocument>("competitions")
                .CountDocumentsAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("ExternalId", 2000L))

        if count = 0 then
            let input: Competitions.CreateInput =
                {
                    Description = "2022. aasta jalgpalli maailmameistrivõistlused"
                    ExternalId = 2000L
                    Date = DateTimeOffset(2022, 11, 20, 16, 0, 0, TimeSpan.Zero)
                }
            let! _ = Dashboard.addCompetitionDom input
            ()
    }

    initCompetition.Wait()

    app


let configureHost (host: IHostBuilder) =
    host.ConfigureAppConfiguration(configureAppConfiguration)
        .ConfigureServices(configureServices)


let configureJsonSerializer () =
    let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    options.Converters.Add(JsonFSharpConverter())
    SystemTextJson.Serializer options


let configureJwtAuthentication (options: JwtBearerOptions) =
    options.MapInboundClaims <- false
    options.TokenValidationParameters <- TokenValidationParameters(
        SignatureValidator = (fun token _ -> JwtSecurityToken(token)),
        ValidateAudience = false,
        ValidateIssuer = false,
        ValidateIssuerSigningKey = false
    )


let app = application {
    no_router
    memory_cache
    use_gzip
    app_config configureApp
    host_config configureHost
    use_json_serializer (configureJsonSerializer())
    use_jwt_authentication_with_config configureJwtAuthentication
}


run app

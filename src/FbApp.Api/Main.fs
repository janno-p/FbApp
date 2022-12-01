module FbApp.Api.Main

open System.IdentityModel.Tokens.Jwt
open System.Text.Json
open System.Text.Json.Serialization
open EventStore.Client
open FbApp.Api
open FbApp.Api.Aggregate
open FbApp.Api.Configuration
open FbApp.Api.Domain
open FbApp.Api.EventStore
open FbApp.Api.LiveUpdate
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.IdentityModel.Tokens
open MongoDB.Bson
open MongoDB.Driver
open Quartz
open Saturn
open Saturn.Endpoint
open System


MongoDbSetup.init()


let mainRouter = router {
    get "/dapr/config" (obj() |> Successful.OK)

    get "/api/competition/status" FbApp.Competitions.Api.getCompetitionStatus
    get "/api/fixtures" FbApp.Fixtures.Api.getDefaultFixture
    getf "/api/fixtures/%O" FbApp.Fixtures.Api.getFixture
    get "/api/prediction" (Auth.mustBeLoggedIn >=> (Auth.withUser FbApp.Predictions.Api.getUserPrediction))
    get "/api/prediction/board" FbApp.PredictionResults.Api.getLeaderboard

    forward "/api/predict" Predict.predictScope
    forward "/api/fixtures" Fixtures.scope
    forward "/api/predictions" Predictions.scope
    forward "/api/leagues" Leagues.scope

    forward "/api" (router {
        pipe_through Auth.mustBeLoggedIn
        pipe_through Auth.mustBeAdmin

        forward "/dashboard" Dashboard.dashboardScope
    })
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

    services.AddQuartz(fun quartz ->
        quartz.UseMicrosoftDependencyInjectionJobFactory()
        quartz.UseSimpleTypeLoader()
        quartz.UseInMemoryStore()

        let jobKey = JobKey("live update")
        quartz.AddJob<LiveUpdateJob>(fun job -> job.WithIdentity(jobKey) |> ignore)
            |> ignore

        quartz.AddTrigger(fun trigger ->
            trigger.WithIdentity("partial update")
                .ForJob(jobKey)
                .UsingJobData("fullUpdate", false)
                .StartAt(DateTimeOffset.UtcNow.AddMinutes(1))
                .WithSimpleSchedule(fun schedule -> schedule.WithIntervalInMinutes(1).RepeatForever() |> ignore)
            |> ignore
        ) |> ignore

        quartz.AddTrigger(fun trigger ->
            trigger.WithIdentity("full update")
                .ForJob(jobKey)
                .UsingJobData("fullUpdate", true)
                .StartNow()
                .WithSimpleSchedule(fun schedule -> schedule.WithIntervalInHours(1).RepeatForever() |> ignore)
            |> ignore
        ) |> ignore
    ) |> ignore

    services.AddQuartzHostedService(fun options ->
        options.WaitForJobsToComplete <- true
    ) |> ignore

    services.AddDaprClient()


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

    FbApp.PredictionResults.ReadModel.registerPredictionResultHandlers
        (app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("PredictionResults.ReadModel"))
        (app.ApplicationServices.GetRequiredService<EventStoreClient>())
        (app.ApplicationServices.GetRequiredService<IOptions<SubscriptionsSettings>>().Value)

    let initCompetition = task {
        use scope = app.ApplicationServices.CreateScope()
        let db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>()

        let! count =
            db.GetCollection<BsonDocument>("competitions")
                .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ExternalId", 2000L))

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

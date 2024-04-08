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
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.IdentityModel.Tokens
open MongoDB.Driver
open System


MongoDbSetup.init()


let endpoints = [
    GET [
        route "/dapr/config" (text "")
        route "/api/competition/status" FbApp.Competitions.Api.getCompetitionStatus
        route "/api/fixtures" FbApp.Fixtures.Api.getDefaultFixture
        routef "/api/fixtures/%O" FbApp.Fixtures.Api.getFixture
        route "/api/prediction" (Auth.mustBeLoggedIn >=> (Auth.withUser FbApp.Predictions.Api.getUserPrediction))
        route "/api/prediction/board" FbApp.PredictionResults.Api.getLeaderboard
    ]

    subRoute "/api/predict" Predict.endpoints
    subRoute "/api/fixtures" Fixtures.endpoints
    subRoute "/api/predictions" Predictions.endpoints
    subRoute "/api/leagues" Leagues.endpoints

    subRoute "/api/dashboard" Dashboard.endpoints
    |> applyBefore Auth.mustBeLoggedIn
    |> applyBefore Auth.mustBeAdmin
]


let initializeMongoDb (sp: IServiceProvider) =
    let configuration = sp.GetService<IConfiguration>()
    let client =
        match configuration.GetConnectionString("mongodb") with
        | null -> MongoClient()
        | value -> MongoClient(value)
    client.GetDatabase("fbapp")


let configureServices (builder: WebApplicationBuilder) =
    let configuration = builder.Configuration
    let services = builder.Services

    services.AddGiraffe() |> ignore
    services.AddRouting() |> ignore

    services.Configure<AuthOptions>(configuration.GetSection("Authentication")) |> ignore
    services.Configure<SubscriptionsSettings>(configuration.GetSection("EventStore:Subscriptions")) |> ignore

    // let eventStoreConnection =
    //     context.Configuration.GetConnectionString("eventstore")
    //     |> Option.ofObj
    //     |> Option.defaultValue (context.Configuration.GetValue("EventStore:Uri"))

    // let setConnectionName (settings: EventStoreClientSettings) =
    //     settings.ConnectionName <- "fbapp"

    // services.AddEventStoreClient(eventStoreConnection, setConnectionName) |> ignore
    // services.AddEventStoreProjectionManagementClient(eventStoreConnection, setConnectionName) |> ignore
    // services.AddEventStorePersistentSubscriptionsClient(eventStoreConnection, setConnectionName) |> ignore

    services.AddSingleton<IMongoDatabase>(initializeMongoDb) |> ignore

    // services.AddQuartz(fun quartz ->
    //     quartz.UseMicrosoftDependencyInjectionJobFactory()
    //     quartz.UseSimpleTypeLoader()
    //     quartz.UseInMemoryStore()
    //
    //     let jobKey = JobKey("live update")
    //     quartz.AddJob<LiveUpdateJob>(fun job -> job.WithIdentity(jobKey) |> ignore)
    //         |> ignore
    //
    //     quartz.AddTrigger(fun trigger ->
    //         trigger.WithIdentity("partial update")
    //             .ForJob(jobKey)
    //             .UsingJobData("fullUpdate", false)
    //             .StartAt(DateTimeOffset.UtcNow.AddMinutes(1))
    //             .WithSimpleSchedule(fun schedule -> schedule.WithIntervalInMinutes(1).RepeatForever() |> ignore)
    //         |> ignore
    //     ) |> ignore
    //
    //     quartz.AddTrigger(fun trigger ->
    //         trigger.WithIdentity("full update")
    //             .ForJob(jobKey)
    //             .UsingJobData("fullUpdate", true)
    //             .StartNow()
    //             .WithSimpleSchedule(fun schedule -> schedule.WithIntervalInHours(1).RepeatForever() |> ignore)
    //         |> ignore
    //     ) |> ignore
    // ) |> ignore

    // services.AddQuartzHostedService(fun options ->
    //     options.WaitForJobsToComplete <- true
    // ) |> ignore

    services.AddDaprClient()
    builder.AddServiceDefaults() |> ignore
    services.AddProblemDetails() |> ignore

    let createJsonSerializer (_: IServiceProvider) : Giraffe.Json.ISerializer =
        let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
        options.Converters.Add(JsonFSharpConverter())
        SystemTextJson.Serializer options

    services.AddSingleton<Giraffe.Json.ISerializer>(createJsonSerializer) |> ignore

    let configureJwtAuthentication (options: JwtBearerOptions) =
        options.MapInboundClaims <- false
        options.TokenValidationParameters <- TokenValidationParameters(
            SignatureValidator = (fun token _ -> JwtSecurityToken(token)),
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = false
        )

    services
        .AddAuthentication(fun options ->
            options.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
            options.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme
        )
        .AddJwtBearer(configureJwtAuthentication)
    |> ignore

    services
        .Configure<GzipCompressionProviderOptions>(fun (opts: GzipCompressionProviderOptions) -> opts.Level <- System.IO.Compression.CompressionLevel.Optimal)
        .AddResponseCompression(fun opts ->
            opts.MimeTypes <- Seq.append ResponseCompressionDefaults.MimeTypes [
                "application/x-yaml";
                "image/svg+xml";
                "application/octet-stream";
                "application/x-font-ttf";
                "application/x-font-opentype";
                "application/x-javascript";
                "text/javascript";
            ])
    |> ignore

    services.AddDistributedMemoryCache() |> ignore
    services.AddSession() |> ignore


let configureAppConfiguration (builder: WebApplicationBuilder) =
    builder.Configuration
        .AddJsonFile("appsettings.user.json", optional=true, reloadOnChange=true)
        .AddEnvironmentVariables()
    |> ignore


let configureApp (app: WebApplication) =
    let forwardedHeaders = ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto

    app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = forwardedHeaders)) |> ignore
    app.UseExceptionHandler() |> ignore

    app.UseSession() |> ignore
    app.UseResponseCompression() |> ignore

    app.UseRouting() |> ignore
    app.UseCloudEvents() |> ignore
    app.UseAuthentication() |> ignore

    app.MapDefaultEndpoints() |> ignore
    app.MapSubscribeHandler() |> ignore
    app.MapGiraffeEndpoints(endpoints)

    let client = app.Services.GetService<EventStoreClient>()

    CommandHandlers.competitionsHandler <-
        makeHandler { Decide = Competitions.decide; Evolve = Competitions.evolve } (makeDefaultRepository client Competitions.AggregateName)

    CommandHandlers.predictionsHandler <-
        makeHandler { Decide = Predictions.decide; Evolve = Predictions.evolve } (makeDefaultRepository client Predictions.AggregateName)

    CommandHandlers.fixturesHandler <-
        makeHandler { Decide = Fixtures.decide; Evolve = Fixtures.evolve } (makeDefaultRepository client Fixtures.AggregateName)

    CommandHandlers.leaguesHandler <-
        makeHandler { Decide = Leagues.decide; Evolve = Leagues.evolve } (makeDefaultRepository client Leagues.AggregateName)

    // let processManagerInitTask =
    //     ProcessManager.connectSubscription app.ApplicationServices

    // processManagerInitTask.Wait()

    // FbApp.PredictionResults.ReadModel.registerPredictionResultHandlers
    //     (app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("PredictionResults.ReadModel"))
    //     (app.ApplicationServices.GetRequiredService<EventStoreClient>())
    //     (app.ApplicationServices.GetRequiredService<IOptions<SubscriptionsSettings>>().Value)

    // let initCompetition = task {
    //     use scope = app.ApplicationServices.CreateScope()
    //     let db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>()
    //
    //     let! count =
    //         db.GetCollection<BsonDocument>("competitions")
    //             .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ExternalId", 2000L))
    //
    //     if count = 0 then
    //         let input: Competitions.CreateInput =
    //             {
    //                 Description = "2022. aasta jalgpalli maailmameistrivõistlused"
    //                 ExternalId = 2000L
    //                 Date = DateTimeOffset(2022, 11, 20, 16, 0, 0, TimeSpan.Zero)
    //             }
    //         let! _ = Dashboard.addCompetitionDom input
    //         ()
    // }

    // initCompetition.Wait()


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureAppConfiguration builder
    configureServices builder

    let app = builder.Build()
    configureApp app

    app.Run()

    0

module FbApp.Api.Main

open System.Text.Json
open System.Text.Json.Serialization
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
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.IdentityModel.Tokens
open MongoDB.Bson
open MongoDB.Driver
open Quartz
open System
open Microsoft.IdentityModel.JsonWebTokens
open KurrentDB.Client
open Microsoft.AspNetCore.Http.Json


MongoDbSetup.init()


let mainRouter = [
    GET [
        route "/api/competition/status" FbApp.Competitions.Api.getCompetitionStatus
        route "/api/fixtures" FbApp.Fixtures.Api.getDefaultFixture
        routef "/api/fixtures/%O" FbApp.Fixtures.Api.getFixture
        route "/api/prediction" (Auth.mustBeLoggedIn >=> Auth.withUser FbApp.Predictions.Api.getUserPrediction)
        route "/api/prediction/board" (FbApp.PredictionResults.Api.getLeaderboard FootballData.ActiveCompetition)
    ]

    subRoute "/api/predict" Predict.predictScope
    subRoute "/api/fixtures" Fixtures.scope
    subRoute "/api/predictions" Predictions.scope
    subRoute "/api/leagues" Leagues.scope

    subRoute "/api" [
        subRoute "/dashboard" Dashboard.dashboardScope
    ]
    |> applyBefore Auth.mustBeLoggedIn
    |> applyBefore Auth.mustBeAdmin
]


let initializeMongoDb (sp: IServiceProvider) =
    let configuration = sp.GetService<IConfiguration>()
    let client =
        match configuration.GetConnectionString "mongodb" with
        | null -> new MongoClient()
        | value -> new MongoClient(value)
    client.GetDatabase "fbapp"


let configureJsonSerializer () =
    let jsonSerializerOptions = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    let JsonFSharpOptions = JsonFSharpOptions.Default()
    Json.FsharpFriendlySerializer(JsonFSharpOptions, jsonSerializerOptions)


let configureJwtAuthentication (options: JwtBearerOptions) =
    options.MapInboundClaims <- false
    options.TokenValidationParameters <- TokenValidationParameters(
        SignatureValidator = (fun token _ -> JsonWebToken token),
        ValidateAudience = false,
        ValidateIssuer = false,
        ValidateIssuerSigningKey = false
    )


let configureServices (builder: WebApplicationBuilder) =
    builder.AddServiceDefaults() |> ignore
    builder.Services.AddRouting() |> ignore

    builder.Services.AddAuthorization() |> ignore
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, configureJwtAuthentication)
    |> ignore

    builder.Services.AddSingleton<Json.ISerializer>(configureJsonSerializer()) |> ignore

    builder.Services
        .Configure(fun (opts: GzipCompressionProviderOptions) -> opts.Level <- IO.Compression.CompressionLevel.Optimal)
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

    builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection "Authentication") |> ignore
    builder.Services.Configure<SubscriptionsSettings>(builder.Configuration.GetSection "EventStore:Subscriptions") |> ignore

    let eventStoreConnection =
        builder.Configuration.GetConnectionString "eventstore"

    let setConnectionName (settings: KurrentDBClientSettings) =
        settings.ConnectionName <- "fbapp"

    builder.Services
        .AddKurrentDBClient(eventStoreConnection, setConnectionName)
        .AddKurrentDBProjectionManagementClient(eventStoreConnection, setConnectionName)
        .AddKurrentDBPersistentSubscriptionsClient(eventStoreConnection, setConnectionName)
    |> ignore

    builder.Services.AddSingleton<IMongoDatabase> initializeMongoDb |> ignore

    builder.Services.AddQuartz(fun quartz ->
        quartz.UseSimpleTypeLoader()
        quartz.UseInMemoryStore()

        let jobKey = JobKey "live update"
        quartz.AddJob<LiveUpdateJob>(fun job -> job.WithIdentity jobKey |> ignore)
            |> ignore

        quartz.AddTrigger(fun trigger ->
            trigger.WithIdentity("partial update")
                .ForJob(jobKey)
                .UsingJobData("fullUpdate", false)
                .StartAt(DateTimeOffset.UtcNow.AddMinutes(2))
                .WithSimpleSchedule(fun schedule -> schedule.WithIntervalInMinutes(1).RepeatForever() |> ignore)
            |> ignore
        ) |> ignore

        quartz.AddTrigger(fun trigger ->
            trigger.WithIdentity("full update")
                .ForJob(jobKey)
                .UsingJobData("fullUpdate", true)
                .StartAt(DateTimeOffset.UtcNow.AddMinutes(1))
                .WithSimpleSchedule(fun schedule -> schedule.WithIntervalInHours(1).RepeatForever() |> ignore)
            |> ignore
        ) |> ignore
    ) |> ignore

    builder.Services.AddQuartzHostedService(fun options ->
        options.WaitForJobsToComplete <- true
    ) |> ignore

    builder.Services.AddDistributedMemoryCache().AddProblemDetails().AddSession().AddGiraffe() |> ignore

    builder.Services.ConfigureHttpJsonOptions(fun jsonOptions ->
        JsonFSharpOptions.Default().AddToJsonSerializerOptions jsonOptions.SerializerOptions
    ) |> ignore


let configureAppConfiguration (builder: WebApplicationBuilder) =
    builder
        .Configuration
        .AddJsonFile("appsettings.json", optional=true, reloadOnChange=true)
        .AddJsonFile($"appsettings.%s{builder.Environment.EnvironmentName}.json", optional=true, reloadOnChange=true)
        .AddJsonFile("appsettings.user.json", optional=true, reloadOnChange=true)
        .AddEnvironmentVariables()
    |> ignore


let configureApp (app: WebApplication) =
    let forwardedHeaders = ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto

    app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = forwardedHeaders))
        .UseExceptionHandler()
        .UseSession()
        .UseResponseCompression()
        .UseStaticFiles()
        .UseRouting()
        .UseAuthentication()
        .UseAuthorization()
        .UseEndpoints(fun endpoints ->
            endpoints.MapGiraffeEndpoints mainRouter
        )
    |> ignore

    app.MapDefaultEndpoints() |> ignore

    let client = app.Services.GetService<KurrentDBClient>()
    let jsonOptions = app.Services.GetService<IOptions<JsonOptions>>().Value.SerializerOptions

    CommandHandlers.competitionsHandler <-
        makeHandler { Decide = Competitions.decide; Evolve = Competitions.evolve } (makeDefaultRepository client Competitions.AggregateName jsonOptions)

    CommandHandlers.predictionsHandler <-
        makeHandler { Decide = Predictions.decide; Evolve = Predictions.evolve } (makeDefaultRepository client Predictions.AggregateName jsonOptions)

    CommandHandlers.fixturesHandler <-
        makeHandler { Decide = Fixtures.decide; Evolve = Fixtures.evolve } (makeDefaultRepository client Fixtures.AggregateName jsonOptions)

    CommandHandlers.leaguesHandler <-
        makeHandler { Decide = Leagues.decide; Evolve = Leagues.evolve } (makeDefaultRepository client Leagues.AggregateName jsonOptions)

    let processManagerInitTask =
        ProcessManager.connectSubscription app.Services

    processManagerInitTask.Wait()

    FbApp.PredictionResults.ReadModel.registerPredictionResultHandlers
        (app.Services.GetRequiredService<ILoggerFactory>().CreateLogger "PredictionResults.ReadModel")
        (app.Services.GetRequiredService<KurrentDBClient>())
        (app.Services.GetRequiredService<IOptions<SubscriptionsSettings>>().Value)
        jsonOptions

    let initCompetition = task {
        use scope = app.Services.CreateScope()
        let db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>()

        let! count =
            db.GetCollection<BsonDocument>("competitions")
                .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ExternalId", FootballData.ActiveCompetition))

        if count = 0 then
            let input: Competitions.CreateInput =
                {
                    Description = "Jalka MM 2026"
                    ExternalId = FootballData.ActiveCompetition
                    Date = DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero)
                }
            let! _ = Dashboard.addCompetitionDom input
            ()
    }

    initCompetition.Wait()


[<EntryPoint>]
let main args =
    let build = WebApplication.CreateBuilder args
    configureAppConfiguration build
    configureServices build

    let app = build.Build()
    configureApp app

    app.Run()

    0

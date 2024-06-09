module FbApp.DbManager

open EvolveDb
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Diagnostics.HealthChecks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Npgsql
open OpenTelemetry
open Polly
open System
open System.Diagnostics
open System.Threading.Tasks


[<Literal>]
let ActivitySourceName = "Migrations"


type DbInitializer(serviceProvider: IServiceProvider, logger: ILogger<DbInitializer>) =
    inherit BackgroundService()

    let activitySource = new ActivitySource(ActivitySourceName)

    let applyMigrations (connection: NpgsqlConnection) =
        let evolve = Evolve(connection, fun msg -> logger.LogInformation("{Message}", msg))
        evolve.IsEraseDisabled <- true
        evolve.OutOfOrder <- true
        evolve.MustEraseOnValidationError <- false
        evolve.EnableClusterMode <- false
        evolve.CommandTimeout <- 1000
        evolve.Locations <- ["Migrations"; "Seeds"]
        evolve.Migrate()

    let createDatabase (connectionString: string) cancellationToken =
        task {
            let data = NpgsqlConnectionStringBuilder(connectionString)

            let database = data.Database
            data.Database <- null

            use connection = new NpgsqlConnection(data.ConnectionString)
            do! connection.OpenAsync(cancellationToken)

            use command = new NpgsqlCommand($"CREATE DATABASE %s{database}", connection)
            let! _ = command.ExecuteNonQueryAsync(cancellationToken)

            ()
        }

    let migrateAndSeed (configuration: IConfiguration) cancellationToken =
        task {
            let connectionString = configuration.GetConnectionString("database")

            logger.LogInformation("Creating database")
            do! createDatabase connectionString cancellationToken

            logger.LogInformation("Running migrations")

            use connection = new NpgsqlConnection(connectionString)
            do! connection.OpenAsync(cancellationToken)

            applyMigrations connection
        }

    override _.ExecuteAsync(cancellationToken) =
        task {
            use scope = serviceProvider.CreateScope()

            use _ = activitySource.StartActivity($"Initializing database", ActivityKind.Client)

            let sw = Stopwatch.StartNew();

            let retryPolicy =
                Policy.Handle<exn>()
                    .WaitAndRetryAsync(
                        5,
                        (fun retryAttempt ->
                            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))),
                        (fun ex retryCount context ->
                            logger.LogError(ex, "Retrying database migration {RetryCount} of {PolicyKey} at {OperationKey}", retryCount, context.PolicyKey, context.OperationKey))
                    )

            let configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>()

            let migration ct =
                migrateAndSeed configuration ct

            do! retryPolicy.ExecuteAsync(migration, cancellationToken)

            logger.LogInformation("Database initialization completed after {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds)
        }


type DbInitializerHealthCheck(dbInitializer: DbInitializer) =
    interface IHealthCheck with
        member _.CheckHealthAsync(_, _) =
            let task = dbInitializer.ExecuteTask
            match task with
            | x when x.IsCompletedSuccessfully -> Task.FromResult(HealthCheckResult.Healthy())
            | x when x.IsFaulted ->
                let innerMessage = x.Exception |> Option.ofObj |> Option.bind (_.InnerException >> Option.ofObj) |> Option.map _.Message |> Option.defaultValue ""
                Task.FromResult(HealthCheckResult.Unhealthy(innerMessage, x.Exception))
            | x when x.IsCanceled -> Task.FromResult(HealthCheckResult.Unhealthy("Database initialization was canceled"))
            | _ -> Task.FromResult(HealthCheckResult.Degraded("Database initialization is still in progress"))


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.AddServiceDefaults() |> ignore

    (builder.Services.AddOpenTelemetry(): IOpenTelemetryBuilder)
        .WithTracing(fun tracing ->
            tracing.AddSource(ActivitySourceName)
            |> ignore)
        |> ignore

    builder.Services.AddSingleton<DbInitializer>() |> ignore
    builder.Services.AddHostedService(fun sp -> sp.GetRequiredService<DbInitializer>()) |> ignore
    builder.Services.AddHealthChecks().AddCheck<DbInitializerHealthCheck>("DbInitializer", Nullable()) |> ignore

    let app = builder.Build()
    app.MapDefaultEndpoints() |> ignore

    app.Run()

    0

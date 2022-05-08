module FbApp.Init.Events.Program


open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open System
open Microsoft.Extensions.Logging
open EventStore.Client
open Microsoft.Extensions.Options
open Grpc.Core


[<CLIMutable>]
type EventsSettings = {
    ApplicationName: string
    ProcessManager: string
    ProjectionName: string
    }


let getQuery (eventsSettings: EventsSettings) =
    $"""fromAll()
.when({{
    $any: function (state, ev) {{
        if (ev.metadata !== null && ev.metadata.applicationName === "%s{eventsSettings.ApplicationName}") {{
            linkTo("%s{eventsSettings.ProjectionName}", ev)
        }}
    }}
}})"""


let configureServices () =
    let services = ServiceCollection()

    services.AddLogging(fun builder -> builder.AddConsole() |> ignore) |> ignore

    let configuration =
        ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional=true)
            .AddEnvironmentVariables()
            .Build()

    services.Configure<EventsSettings>(configuration.GetSection("Events")) |> ignore

    let connectionString = configuration.GetConnectionString("Default")
    services.AddEventStoreProjectionManagementClient(connectionString) |> ignore
    services.AddEventStorePersistentSubscriptionsClient(connectionString) |> ignore

    services.BuildServiceProvider()


let (|Conflict|_|) (ex: exn) =
    match ex with
    | :? InvalidOperationException as ex ->
        match ex.InnerException with
        | :? RpcException as e when e.StatusCode = StatusCode.Unknown && e.Status.Detail = "Envelope callback expected Updated, received Conflict instead" ->
            Some ()
        | _ ->
            None
    | _ ->
        None


let (|AlreadyExists|_|) (ex: exn) =
    match ex with
    | :? InvalidOperationException as ex ->
        match ex.InnerException with
        | :? RpcException as e when e.StatusCode = StatusCode.AlreadyExists ->
            Some ()
        | _ ->
            None
    | _ ->
        None


[<EntryPoint>]
let main _ =
    let serviceProvider = configureServices()

    let logger = serviceProvider.GetRequiredService<ILogger<obj>>()
    let projectionManagementClient = serviceProvider.GetRequiredService<EventStoreProjectionManagementClient>()
    let subscriptionsClient = serviceProvider.GetRequiredService<EventStorePersistentSubscriptionsClient>()

    let eventsSettings = serviceProvider.GetRequiredService<IOptions<EventsSettings>>().Value

    let task = task {
        try
            logger.LogInformation($"Trying to create '%s{eventsSettings.ProjectionName}' projection (if not exists)")
            do! projectionManagementClient.CreateContinuousAsync(eventsSettings.ProjectionName, getQuery eventsSettings)
        with
        | Conflict ->
            logger.LogInformation($"Event projection '%s{eventsSettings.ProjectionName}' already exists")
        | e ->
            logger.LogCritical(e, $"Error occurred while initializing '%s{eventsSettings.ProjectionName}' projection")
            raise e

        let settings =
            PersistentSubscriptionSettings(
                resolveLinkTos = true,
                startFrom = StreamPosition.Start,
                checkPointLowerBound = 1
            )

        try
            logger.LogInformation($"Trying to create '%s{eventsSettings.ProcessManager}' subscription group ...")
            do! subscriptionsClient.CreateAsync(eventsSettings.ProjectionName, eventsSettings.ProcessManager, settings)
        with
        | AlreadyExists ->
            logger.LogInformation($"Subscription group '%s{eventsSettings.ProcessManager}' already exists")
        | e ->
            logger.LogCritical(e, $"Error occurred while initializing '%s{eventsSettings.ProcessManager}' subscription group")
            raise e
    }

    task.Wait()

    0

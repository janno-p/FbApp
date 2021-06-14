module FbApp.Init.Events.Program


open FSharp.Control.Tasks
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open System
open Microsoft.Extensions.Logging
open EventStore.Client
open Microsoft.Extensions.Options


[<CLIMutable>]
type ProjectionsSettings = {
    ApplicationName: string
    DomainEventsName: string
    }


[<CLIMutable>]
type SubscriptionGroupsSettings = {
    Projections: string
    ProcessManager: string
    }


let getEnvironmentVariableOrDefault defaultValue name =
        match Environment.GetEnvironmentVariable(name) |> Option.ofObj with
        | Some value -> value
        | None -> defaultValue


let getQuery (projectionsSettings: ProjectionsSettings) =
    (sprintf """fromAll()
.when({
    $any: function (state, ev) {
        if (ev.metadata !== null && ev.metadata.applicationName === "%s") {
            linkTo("%s", ev)
        }
    }
})""" projectionsSettings.ApplicationName projectionsSettings.DomainEventsName)


let configureServices () =
    let services = ServiceCollection()

    services.AddLogging(fun builder -> builder.AddConsole() |> ignore) |> ignore

    let configuration =
        ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional=true)
            .AddEnvironmentVariables()
            .Build()

    services.Configure<ProjectionsSettings>(configuration.GetSection("Projections")) |> ignore
    services.Configure<SubscriptionGroupsSettings>(configuration.GetSection("SubscriptionGroups")) |> ignore

    let connectionString = configuration.GetConnectionString("Default")
    services.AddEventStoreProjectionManagementClient(connectionString) |> ignore
    services.AddEventStorePersistentSubscriptionsClient(connectionString) |> ignore

    services.BuildServiceProvider()


[<EntryPoint>]
let main _ =
    let serviceProvider = configureServices()

    let logger = serviceProvider.GetRequiredService<ILogger<obj>>()
    let projectionManagementClient = serviceProvider.GetRequiredService<EventStoreProjectionManagementClient>()
    let subscriptionsClient = serviceProvider.GetRequiredService<EventStorePersistentSubscriptionsClient>()

    let projectionsSettings = serviceProvider.GetRequiredService<IOptions<ProjectionsSettings>>().Value
    let subscriptionGroupsSettings = serviceProvider.GetRequiredService<IOptions<SubscriptionGroupsSettings>>().Value

    let task = unitTask {
        try
            logger.LogInformation($"Trying to create '%s{projectionsSettings.DomainEventsName}' projection (if not exists)")
            do! projectionManagementClient.CreateContinuousAsync(projectionsSettings.DomainEventsName, getQuery projectionsSettings)
        with e ->
            logger.LogWarning(e, $"Error occurred while initializing '%s{projectionsSettings.DomainEventsName}' projection")

        let settings = PersistentSubscriptionSettings(resolveLinkTos = true, startFrom = StreamPosition.Start)

        try
            logger.LogInformation($"Trying to create '%s{subscriptionGroupsSettings.Projections}' subscription group (if not exists)")
            do! subscriptionsClient.CreateAsync(projectionsSettings.DomainEventsName, subscriptionGroupsSettings.Projections, settings)
        with e ->
            logger.LogWarning(e, $"Error occurred while initializing '%s{subscriptionGroupsSettings.Projections}' subscription group")

        try
            logger.LogInformation($"Trying to create '%s{subscriptionGroupsSettings.ProcessManager}' subscription group (if not exists)")
            do! subscriptionsClient.CreateAsync(projectionsSettings.DomainEventsName, subscriptionGroupsSettings.ProcessManager, settings)
        with e ->
            logger.LogWarning(e, $"Error occurred while initializing '%s{subscriptionGroupsSettings.ProcessManager}' subscription group")
    }

    task.Wait()

    0

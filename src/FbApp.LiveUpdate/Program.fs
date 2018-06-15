﻿module FbApp.LiveUpdate.Program

open EventStore.ClientAPI
open FbApp.Core
open FbApp.Domain
open FSharp.Control.Tasks.ContextInsensitive
open System
open System.IO
open System.Threading
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open FbApp.LiveUpdate.Configuration
open FbApp.Core.EventStore
open Microsoft.Extensions.Configuration.UserSecrets

[<assembly: UserSecretsIdAttribute("d6072641-6e1a-4bbc-bbb6-d355f0e38db4")>]
do ()

type FixturesHandler = Aggregate.CommandHandler<Fixtures.Id, Fixtures.Command, unit>
type Marker = class end

let updateFixtures authToken competitionId (log: ILogger) filters (fixtureHandler: FixturesHandler) = task {
    let! result = FootballData.getCompetitionFixtures authToken 467L filters
    match result with
    | Ok(data) ->
        log.LogInformation("Loaded data of {0} fixtures.", data.Count)
        for fixture in data.Fixtures do
            let id = Fixtures.Id (competitionId, fixture.Id)
            let command =
                Fixtures.UpdateFixture
                    {
                        Status =
                            fixture.Status
                        Result =
                            fixture.Result
                            |> Option.bind (fun x ->
                                match (x.GoalsHomeTeam, x.GoalsAwayTeam) with
                                | Some(x1), Some(x2) -> Some(x1, x2)
                                | _ -> None
                            )
                    }
            let! _ = fixtureHandler (id, None) command
            ()
    | Error(statusCode, statusText, error) ->
        log.LogWarning("Failed to load fixture data: ({0}: {1}) - {2}", statusCode, statusText, error.Error)
}

[<EntryPoint>]
let main _ =
    Console.OutputEncoding <- Text.Encoding.UTF8

    let environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")

    let configuration =
        let builder =
            ConfigurationBuilder()
                .SetBasePath(Path.Combine(AppContext.BaseDirectory))
                .AddJsonFile("appsettings.json", optional = false)
        if environment |> String.IsNullOrWhiteSpace |> not then
            builder
                .AddJsonFile(sprintf "appsettings.%s.json" environment, optional = true)
                .AddUserSecrets<Marker>()
            |> ignore
        builder.Build()

    use loggerFactory =
        (new LoggerFactory())
            .AddConsole(configuration.GetSection("Logging"))
            .AddDebug()

    let authOptions = Activator.CreateInstance<AuthOptions>()
    configuration.Bind("Authentication", authOptions)

    let eventStoreOptions = Activator.CreateInstance<EventStoreOptions>()
    configuration.Bind("EventStore", eventStoreOptions)

    let log = loggerFactory.CreateLogger()

    let authToken = authOptions.FootballDataToken
    let connection = (EventStore.createEventStoreConnection eventStoreOptions).Result
    let competitionId = Guid.Parse(configuration.["CompetitionId"])

    let fixtureHandler =
        Aggregate.makeHandler
            { InitialState = Fixtures.initialState; Decide = Fixtures.decide; Evolve = Fixtures.evolve; StreamId = Fixtures.streamId }
            (EventStore.makeDefaultRepository connection Fixtures.AggregateName)

    let mutable lastFullUpdate = DateTime.MinValue

    while true do
        let filters =
            let now = DateTime.Now
            if lastFullUpdate.AddHours(1.0) < now then
                lastFullUpdate <- now
                []
            else [FootballData.TimeFrameRange(now.Date, now.Date)]

        updateFixtures authToken competitionId log filters fixtureHandler
        |> Async.AwaitTask
        |> Async.RunSynchronously

        Thread.Sleep(TimeSpan.FromMinutes(1.0))

    0

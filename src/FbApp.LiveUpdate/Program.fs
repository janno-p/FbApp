module FbApp.LiveUpdate.Program

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

type FixturesHandler = Aggregate.CommandHandler<Fixtures.Id, Fixtures.Command, Fixtures.Error>
type Marker = class end

let mutable lastFullUpdate = DateTimeOffset.MinValue
let [<Literal>] competitionId = 467L

let updateFixtures authToken (log: ILogger) (fixtureHandler: FixturesHandler) = task {
    let filters, onSuccess =
        let now = DateTimeOffset.UtcNow
        if lastFullUpdate.AddHours(1.0) < now then
            [], (fun () -> lastFullUpdate <- now)
        else
            let today = DateTimeOffset(now.Date, TimeSpan.Zero)
            [FootballData.TimeFrameRange(today, today)], (fun () -> ())
    let! result = FootballData.getCompetitionFixtures authToken competitionId filters
    match result with
    | Ok(data) ->
        log.LogInformation("Loaded data of {0} fixtures.", data.Count)
        let mutable anyError = false
        let competitionGuid = competitionId |> Competitions.streamId
        for fixture in data.Fixtures |> Array.filter (fun f -> f.Matchday < 4) do
            let id = Fixtures.Id (competitionGuid, fixture.Id)
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
            let! updateResult = fixtureHandler (id, Aggregate.Any) command
            match updateResult with
            | Ok(_) -> ()
            | Error(err) ->
                anyError <- true
                log.LogError(sprintf "Could not update fixture %A: %A" id err)
        if not anyError then onSuccess ()
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

    let fixtureHandler =
        Aggregate.makeHandler
            { Decide = Fixtures.decide; Evolve = Fixtures.evolve; StreamId = Fixtures.streamId }
            (EventStore.makeDefaultRepository connection Fixtures.AggregateName)

    while true do
        try
            updateFixtures authToken log fixtureHandler
            |> Async.AwaitTask
            |> Async.RunSynchronously
        with e -> log.LogError(e, "Exception occured while updating fixtures.")

        Thread.Sleep(TimeSpan.FromMinutes(1.0))

    0

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

type FixturesHandler = Aggregate.CommandHandler<Fixtures.Command, Fixtures.Error>
type Marker = class end

let mutable lastFullUpdate = DateTimeOffset.MinValue
let [<Literal>] oldCompetitionId = 467L
let [<Literal>] newCompetitionId = 2000L

let mapResult (fixture: FootballData.CompetitionFixture) =
    fixture.Result
    |> Option.bind (fun x ->
        match (x.GoalsHomeTeam, x.GoalsAwayTeam) with
        | Some(x1), Some(x2) -> Some(x1, x2)
        | _ -> None
    )

let mapTeamId = function
    | 6679L -> 788L
    | x -> x

let mapFixtureId = function
    | 200000L -> 165069L
    | 200001L -> 165084L
    | 200006L -> 165083L
    | 200007L -> 165076L
    | 200012L -> 165072L
    | 200018L -> 165073L
    | 200013L -> 165071L
    | 200019L -> 165074L
    | 200024L -> 165075L
    | 200030L -> 165082L
    | 200025L -> 165070L
    | 200031L -> 165081L
    | 200036L -> 165077L
    | 200037L -> 165078L
    | 200042L -> 165080L
    | 200043L -> 165079L
    | 200002L -> 165100L
    | 200008L -> 165087L
    | 200003L -> 165086L
    | 200009L -> 165085L
    | 200014L -> 165099L
    | 200015L -> 165096L
    | 200020L -> 165094L
    | 200026L -> 165092L
    | 200021L -> 165098L
    | 200027L -> 165091L
    | 200038L -> 165088L
    | 200032L -> 165089L
    | 200033L -> 165090L
    | 200039L -> 165093L
    | 200044L -> 165095L
    | 200045L -> 165097L
    | 200004L -> 165111L
    | 200005L -> 165101L
    | 200010L -> 165112L
    | 200011L -> 165109L
    | 200016L -> 165107L
    | 200017L -> 165113L
    | 200022L -> 165115L
    | 200023L -> 165114L
    | 200034L -> 165106L
    | 200035L -> 165102L
    | 200028L -> 165116L
    | 200029L -> 165108L
    | 200046L -> 165104L
    | 200047L -> 165103L
    | 200040L -> 165105L
    | 200041L -> 165110L
    | x -> x

let mapGoals : FootballData.Api2.CompetitionMatchScoreGoals -> FbApp.Domain.Fixtures.FixtureGoals option = function
    | { HomeTeam = Some(a); AwayTeam = Some(b) } -> Some({ Home = a; Away = b })
    | _ -> None

let updateFixtures authToken (log: ILogger) (fixtureHandler: FixturesHandler) = task {
    let filters, onSuccess =
        let now = DateTimeOffset.UtcNow
        if lastFullUpdate.AddHours(1.0) < now then
            [], (fun () -> lastFullUpdate <- now)
        else
            let today = DateTimeOffset(now.Date, TimeSpan.Zero)
            [FootballData.Api2.DateRange(today, today)], (fun () -> ())
    let! result = FootballData.Api2.getCompetitionMatches authToken newCompetitionId filters
    match result with
    | Ok(data) ->
        log.LogInformation("Loaded data of {0} fixtures.", data.Count)
        let mutable anyError = false
        let competitionGuid = Competitions.createId oldCompetitionId
        for fixture in data.Matches |> Array.filter (fun f -> f.Stage = "GROUP_STAGE") do
            let id = Fixtures.createId (competitionGuid, mapFixtureId fixture.Id)
            let command =
                Fixtures.UpdateFixture
                    {
                        Status = fixture.Status
                        FullTime = mapGoals fixture.Score.FullTime
                        ExtraTime = mapGoals fixture.Score.ExtraTime
                        Penalties = mapGoals fixture.Score.Penalties
                    }
            let! updateResult = fixtureHandler (id, Aggregate.Any) command
            match updateResult with
            | Ok(_) -> ()
            | Error(err) ->
                anyError <- true
                log.LogError(sprintf "Could not update fixture %A: %A" id err)
        for fixture in data.Matches |> Array.filter (fun f -> f.Stage = "ROUND_OF_16" || f.Stage = "QUARTER_FINALS" || f.Stage = "SEMI_FINALS" || f.Stage = "FINAL") do
            let fixtureId = mapFixtureId fixture.Id
            let id = Fixtures.createId (competitionGuid, fixtureId)
            let command =
                Fixtures.UpdateQualifiers
                    {
                        CompetitionId = competitionGuid
                        ExternalId = fixtureId
                        HomeTeamId = mapTeamId fixture.HomeTeam.Id
                        AwayTeamId = mapTeamId fixture.AwayTeam.Id
                        Date = fixture.UtcDate
                        Stage = fixture.Stage
                        Status = fixture.Status
                        FullTime = mapGoals fixture.Score.FullTime
                        ExtraTime = mapGoals fixture.Score.ExtraTime
                        Penalties = mapGoals fixture.Score.Penalties
                    }
            let! _ = fixtureHandler (id, Aggregate.Any) command
            ()
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
        Aggregate.makeHandler { Decide = Fixtures.decide; Evolve = Fixtures.evolve } (EventStore.makeDefaultRepository connection Fixtures.AggregateName)

    while true do
        try
            updateFixtures authToken log fixtureHandler
            |> Async.AwaitTask
            |> Async.RunSynchronously
        with e -> log.LogError(e, "Exception occured while updating fixtures.")

        Thread.Sleep(TimeSpan.FromMinutes(1.0))

    0

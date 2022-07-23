namespace FbApp.LiveUpdate


open Dapr.Client
open Flurl.Http
open Flurl.Http.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FSharp.Reflection
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System
open System.Threading.Tasks
open System.Collections.Generic


type OptionConverter () =
    inherit JsonConverter()

    override _.CanConvert typ =
        typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<option<_>>

    override _.WriteJson (writer, value, serializer) =
        let value =
            if value |> isNull then null else
            let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
            fields[0]
        serializer.Serialize(writer, value)

    override _.ReadJson(reader, typ, _, serializer) =
        let innerType = typ.GetGenericArguments().[0]
        let innerType =
            if innerType.IsValueType then typedefof<Nullable<_>>.MakeGenericType([|innerType|])
            else innerType
        let value = serializer.Deserialize(reader, innerType)
        let cases = FSharpType.GetUnionCases(typ)
        if value |> isNull then FSharpValue.MakeUnion(cases[0], [||])
        else FSharpValue.MakeUnion(cases[1], [|value|])


[<CLIMutable>]
type ApiSettings = {
    BaseUrl: string
    Token: string
    CompetitionId: int64
    }


[<CLIMutable>]
type GoalsDto = {
    HomeTeam: int option
    AwayTeam: int option
    }


[<CLIMutable>]
type ScoreDto = {
    Winner: string option
    Duration: string
    FullTime: GoalsDto
    HalfTime: GoalsDto
    ExtraTime: GoalsDto
    Penalties: GoalsDto
    }


[<CLIMutable>]
type TeamDto = {
    Id: int64 option
    Name: string option
    }


[<CLIMutable>]
type RefereeDto = {
    Id: int64
    Name: string
    Role: string
    Nationality: string
    }


[<CLIMutable>]
type MatchDto = {
    Id: int64
    UtcDate: DateTimeOffset
    Status: string
    Matchday: int
    Stage: string
    Group: string option
    LastUpdated: DateTimeOffset
    Score: ScoreDto
    HomeTeam: TeamDto
    AwayTeam: TeamDto
    Referees: RefereeDto array
    }


[<CLIMutable>]
type MatchesDto = {
    Count: int
    Matches: MatchDto array
    }


type FixtureDto = {
    FixtureId: int64
    CompetitionId: int64
    HomeTeamId: int64 option
    AwayTeamId: int64 option
    UtcDate: DateTimeOffset
    Stage: string
    Status: string
    FullTime: (int * int) option
    HalfTime: (int * int) option
    ExtraTime: (int * int) option
    Penalties: (int * int) option
    Winner: string option
    Duration: string
    }


type FixturesUpdatedIntegrationEvent = {
    Fixtures: FixtureDto[]
    }


type Worker(logger: ILogger<Worker>, apiSettings: IOptions<ApiSettings>, dapr: DaprClient) =
    inherit BackgroundService()

    let authToken = apiSettings.Value.Token
    let baseUrl = apiSettings.Value.BaseUrl
    let competitionId = apiSettings.Value.CompetitionId

    let [<Literal>] StoreName = "live-update-state"
    let [<Literal>] PubsubName = "live-update-pubsub"
    let [<Literal>] FixtureUpdatesTopicName = "fixture-updates"

    let competitionKey id =
        $"competition-%d{id}"

    let fixtureKey id =
        $"fixture-%d{id}"

    let isFullUpdate, setLastUpdate =
        let mutable lastFullUpdate = DateTimeOffset.MinValue
        let isFullUpdate now =
            lastFullUpdate.AddHours(1.0) < now
        let setLastUpdate () =
            let now = DateTimeOffset.UtcNow
            if isFullUpdate now then
                lastFullUpdate <- now
        (isFullUpdate, setLastUpdate)

    let getCompetitionMatches cancellationToken = task {
        try
            let filters =
                let now = DateTimeOffset.UtcNow
                if isFullUpdate now then
                    ""
                else
                    let today = DateTimeOffset(now.Date, TimeSpan.Zero).ToString("yyyy-MM-dd")
                    $"?dateFrom=%s{today}&dateTo=%s{today}"
            let url = $"%s{baseUrl}competitions/%d{competitionId}/matches%s{filters}"
            let! matches =
                url.WithHeader("X-Auth-Token", authToken)
                    .GetJsonAsync<MatchesDto>(cancellationToken)
            return Ok(matches)
        with e ->
            return Error(e)
    }

    let updateFixtures cancellationToken = task {
        match! getCompetitionMatches cancellationToken with
        | Ok matches ->
            logger.LogInformation("Loaded data of {count} fixtures.", matches.Count)

            let! fixtureUpdatesLookup, tag =
                dapr.GetStateAndETagAsync<Dictionary<int64, DateTimeOffset>>(
                    StoreName,
                    competitionKey competitionId,
                    cancellationToken = cancellationToken
                )

            let fixtureUpdatesLookup =
                match fixtureUpdatesLookup with
                | null -> Dictionary<int64, DateTimeOffset>()
                | value -> value

            let isUpdated (fixture: MatchDto) =
                match fixtureUpdatesLookup.TryGetValue fixture.Id with
                | false, v | true, v when v < fixture.LastUpdated -> true
                | _ -> false

            let fixtureUpdates = ResizeArray<FixtureDto * string>()

            let mapGoals (g: GoalsDto) =
                match g.HomeTeam, g.AwayTeam with
                | Some(home), Some(away) -> Some(home, away)
                | _ -> None

            for fixture in matches.Matches |> Array.filter isUpdated do
                let! previousFixture, tag =
                    dapr.GetStateAndETagAsync<FixtureDto>(
                        StoreName,
                        fixtureKey fixture.Id,
                        cancellationToken = cancellationToken
                    )

                let newFixture: FixtureDto = {
                    FixtureId = fixture.Id
                    CompetitionId = competitionId
                    HomeTeamId = fixture.HomeTeam.Id
                    AwayTeamId = fixture.AwayTeam.Id
                    UtcDate = fixture.UtcDate
                    Stage = fixture.Stage
                    Status = fixture.Status
                    FullTime = mapGoals fixture.Score.FullTime
                    HalfTime = mapGoals fixture.Score.HalfTime
                    ExtraTime = mapGoals fixture.Score.ExtraTime
                    Penalties = mapGoals fixture.Score.Penalties
                    Winner = fixture.Score.Winner
                    Duration = fixture.Score.Duration
                }

                if newFixture <> previousFixture then
                    fixtureUpdates.Add((newFixture, tag))

                fixtureUpdatesLookup[fixture.Id] <- fixture.LastUpdated

            if fixtureUpdates.Count > 0 then
                do! dapr.PublishEventAsync<FixturesUpdatedIntegrationEvent>(
                        PubsubName,
                        FixtureUpdatesTopicName,
                        { Fixtures = fixtureUpdates |> Seq.map fst |> Seq.toArray },
                        cancellationToken
                    )
                let! _ =
                    dapr.TrySaveStateAsync(
                        StoreName,
                        competitionKey competitionId,
                        fixtureUpdatesLookup,
                        tag,
                        cancellationToken = cancellationToken
                    )
                for f, t in fixtureUpdates do
                    let! _ = dapr.TrySaveStateAsync(StoreName, fixtureKey f.FixtureId, f, t, cancellationToken = cancellationToken)
                    ()

            setLastUpdate()

        | Error e ->
            logger.LogCritical(e, "Error returned from API request")
    }

    override _.ExecuteAsync(cancellationToken) = task {
        while not cancellationToken.IsCancellationRequested do
            logger.LogInformation("Fixture updating worker running at: {time}", DateTimeOffset.Now)

            try
                do! updateFixtures cancellationToken
            with e ->
                logger.LogError(e, "Exception occured while updating fixtures.")

            do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
    }


module Program =
    FlurlHttp.Configure(fun settings ->
        let serializer = JsonSerializerSettings()
        serializer.Converters.Add(OptionConverter())
        serializer.ContractResolver <- CamelCasePropertyNamesContractResolver()
        settings.JsonSerializer <- NewtonsoftJsonSerializer(serializer)
    )

    let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
        services.Configure<ApiSettings>(context.Configuration.GetSection("Api")) |> ignore
        services.AddDaprClient()
        services.AddHostedService<Worker>() |> ignore

    let createHostBuilder args =
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(configureServices)

    [<EntryPoint>]
    let main args =
        createHostBuilder(args).Build().Run()
        0

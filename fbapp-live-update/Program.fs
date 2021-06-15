namespace FbApp.LiveUpdate


open Flurl.Http
open FSharp.Control.Tasks
open System
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Newtonsoft.Json
open Microsoft.FSharp.Reflection
open Newtonsoft.Json.Serialization
open Flurl.Http.Configuration


type OptionConverter () =
    inherit JsonConverter()

    override _.CanConvert (typ) =
        typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<option<_>>

    override _.WriteJson (writer, value, serializer) =
        let value =
            if value |> isNull then null else
            let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
            fields.[0]
        serializer.Serialize(writer, value)

    override __.ReadJson(reader, typ, _, serializer) =
        let innerType = typ.GetGenericArguments().[0]
        let innerType =
            if innerType.IsValueType then typedefof<Nullable<_>>.MakeGenericType([|innerType|])
            else innerType
        let value = serializer.Deserialize(reader, innerType)
        let cases = FSharpType.GetUnionCases(typ)
        if value |> isNull then FSharpValue.MakeUnion(cases.[0], [||])
        else FSharpValue.MakeUnion(cases.[1], [|value|])


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


type Worker(logger: ILogger<Worker>, apiSettings: IOptions<ApiSettings>) =
    inherit BackgroundService()

    let authToken = apiSettings.Value.Token
    let baseUrl = apiSettings.Value.BaseUrl
    let competitionId = apiSettings.Value.CompetitionId

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

    let updateFixtures cancellationToken = unitTask {
        match! getCompetitionMatches cancellationToken with
        | Ok matches ->
            logger.LogInformation("Loaded data of {count} fixtures.", matches.Count)
            (*
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
            if not anyError then setLastUpdate ()
            *)
        | Error e ->
            logger.LogCritical(e, "Error returned from API request")
    }

    override _.ExecuteAsync(cancellationToken) = unitTask {
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
        services.AddHostedService<Worker>() |> ignore

    let createHostBuilder args =
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(configureServices)

    [<EntryPoint>]
    let main args =
        createHostBuilder(args).Build().Run()
        0


(*
let mapResult (fixture: FootballData.CompetitionFixture) =
    fixture.Result
    |> Option.bind (fun x ->
        match (x.GoalsHomeTeam, x.GoalsAwayTeam) with
        | Some(x1), Some(x2) -> Some(x1, x2)
        | _ -> None
    )

let mapGoals : FootballData.Api2.CompetitionMatchScoreGoals -> FbApp.Domain.Fixtures.FixtureGoals option = function
    | { HomeTeam = Some(a); AwayTeam = Some(b) } -> Some({ Home = a; Away = b })
    | _ -> None
*)

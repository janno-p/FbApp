[<RequireQualifiedAccess>]
module FbApp.Api.FootballData

open System
open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading

[<Literal>]
let EuropeanChampionship = 2018L

[<Literal>]
let WorldCup = 2000L

[<Literal>]
let ActiveCompetition = WorldCup

[<Literal>]
let PlayOffStage = "LAST_32" // "LAST_16"

[<CLIMutable>]
type Error =
    {
        Error: string
    }

type Id =
    int64

[<CLIMutable>]
type Link =
    {
        Href: string
    }

let private parseIdSuffix (link: Link) =
    let index = link.Href.LastIndexOf("/")
    let value = link.Href.Substring(index + 1)
    Convert.ToInt64(value)

[<CLIMutable>]
type GroupEntryTeam = {
    Id: int64
    Name: string
    }

[<CLIMutable>]
type GroupEntry =
    {
        Position: int
        Team: GroupEntryTeam
        PlayedGames: int
        Points: int
        Won: int
        Draw: int
        Lost: int
        GoalsFor: int
        GoalsAgainst: int
        GoalDifference: int
    }

[<CLIMutable>]
type LeagueEntryLinks =
    {
        Team: Link
    }

[<CLIMutable>]
type LeagueEntryStats =
    {
        Goals: int
        GoalsAgainst: int
        Wins: int
        Draws: int
        Losses: int
    }

[<CLIMutable>]
type LeagueEntry =
    {
        Position: int
        TeamName: string
        PlayedGames: int
        Points: int
        Goals: int
        GoalsAgainst: int
        GoalDifference: int
        Wins: int
        Draws: int
        Losses: int
        Home: LeagueEntryStats
        Away: LeagueEntryStats
    }

[<CLIMutable>]
type CompetitionLeagueTableStandings = {
    Stage: string
    Group: string
    Table: GroupEntry array
    }

[<CLIMutable>]
type CompetitionLinks =
    {
        Self: Link
        Teams: Link
        Fixtures: Link
        LeagueTable: Link
    }

[<CLIMutable>]
type CompetitionTeamsLinks =
    {
        Self: Link
        Competition: Link
    }

[<CLIMutable>]
type CompetitionTeamLinks =
    {
        Self: Link
        Fixtures: Link
        Players: Link
    }

[<CLIMutable>]
type CompetitionFixturesLinks =
    {
        Self: Link
        Competition: Link
    }

[<CLIMutable>]
type CompetitionFixtureLinks =
    {
        Self: Link
        Competition: Link
        HomeTeam: Link
        AwayTeam: Link
    }

[<CLIMutable>]
type TeamFixturesLinks =
    {
        Self: Link
        Team: Link
    }

[<CLIMutable>]
type TeamPlayersLinks =
    {
        Self: Link
        Team: Link
    }

[<CLIMutable>]
type Competition =
    {
        Id: Id
        Name: string
        // League: string
        // Year: string
        // CurrentMatchday: int
        // NumberOfMatchdays: int
        // NumberOfTeams: int
        // NumberOfGames: int
        LastUpdated: DateTimeOffset
    }

[<CLIMutable>]
type CompetitionList =
    {
        Count: int
        Competitions: Competition array
    }

[<CLIMutable>]
type CompetitionPlayer =
    {
        Id: int64
        Name: string
        Position: string
    }

[<CLIMutable>]
type CompetitionTeam =
    {
        Id: int64
        Name: string
        Tla: string
        ShortName: string
        [<JsonPropertyName("squad")>] Players: CompetitionPlayer array
    }

[<CLIMutable>]
type CompetitionTeams =
    {
        Count: int
        Teams: CompetitionTeam array
    }

[<CLIMutable>]
type CompetitionLeagueTable =
    {
        Standings: CompetitionLeagueTableStandings array
    }

[<CLIMutable>]
type FixtureResultGoals =
    {
        GoalsHomeTeam: int
        GoalsAwayTeam: int
    }

[<AllowNullLiteral>]
type FixtureScore() =
    member val Home = Nullable<int>() with get, set
    member val Away = Nullable<int>() with get, set

[<AllowNullLiteral>]
type FixtureResult() =
    member val Winner = Unchecked.defaultof<string> with get, set
    member val Duration = Unchecked.defaultof<string> with get, set
    member val FullTime = Unchecked.defaultof<FixtureScore> with get, set
    member val HalfTime = Unchecked.defaultof<FixtureScore> with get, set
    member val ExtraTime = Unchecked.defaultof<FixtureScore> with get, set
    member val Penalties = Unchecked.defaultof<FixtureScore> with get, set

[<CLIMutable>]
type CompetitionFixtureTeam = {
    Id: int64 option
    Name: string option
    }

[<CLIMutable>]
type CompetitionFixture =
    {
        Id: int64
        [<JsonPropertyName("utcDate")>] Date: DateTimeOffset
        Status: string
        Matchday: int option
        HomeTeam: CompetitionFixtureTeam option
        AwayTeam: CompetitionFixtureTeam option
        [<JsonPropertyName("score")>] Result: FixtureResult option
        Stage: string
        LastUpdated: DateTimeOffset
        //Odds
    }

[<CLIMutable>]
type CompetitionFixturesResultSet =
    {
        Count: int
    }

[<CLIMutable>]
type CompetitionFixtures =
    {
        ResultSet: CompetitionFixturesResultSet
        [<JsonPropertyName("matches")>] Fixtures: CompetitionFixture array
    }

[<CLIMutable>]
type Fixtures =
    {
        TimeFrameStart: DateTimeOffset
        TimeFrameEnd: DateTimeOffset
        Count: int
        Fixtures: CompetitionFixture array
    }

[<CLIMutable>]
type HeadToHead =
    {
        Count: int
        TimeFrameStart: DateTimeOffset
        TimeFrameEnd: DateTimeOffset
        HomeTeamWins: int
        AwayTeamWins: int
        Draws: int
        LastHomeWinHomeTeam: CompetitionFixture option
        LasWinHomeTeam: CompetitionFixture option
        LastAwayWinAwayTeam: CompetitionFixture option
        LastWinAwayTeam: CompetitionFixture option
        Fixtures: CompetitionFixture array
    }

[<CLIMutable>]
type Fixture =
    {
        Fixture: CompetitionFixture
        [<JsonPropertyName("head2head")>] HeadToHead: HeadToHead
    }

[<CLIMutable>]
type TeamFixtures =
    {
        Season: int
        Count: int
        Fixtures: CompetitionFixture array
    }

[<CLIMutable>]
type TeamPlayer =
    {
        Name: string
        Position: string
        JerseyNumber: int
        DateOfBirth: DateTimeOffset
        Nationality: string
        ContractUntil: DateTimeOffset option
        MarketValue: string
    }

[<CLIMutable>]
type TeamPlayers =
    {
        Count: int
        Players: TeamPlayer array
    }

type TimeFrame =
    | Previous of int
    | Next of int
with
    override this.ToString() =
        match this with
        | Previous v -> $"p%d{v}"
        | Next v -> $"n%d{v}"

type CompetitionFixtureFilter =
    | Range of DateTimeOffset * DateTimeOffset
    | Matchday of int
with
    override this.ToString() =
        match this with
        | Range (s, e) -> sprintf "dateFrom=%s&dateTo=%s" (s.ToString("yyyy-MM-dd")) (e.ToString("yyyy-MM-dd"))
        | Matchday n -> $"matchday=%d{n}"

type FixturesFilter =
    | TimeFrame of TimeFrame
    | League of string
with
    override this.ToString() =
        match this with
        | TimeFrame tf -> $"timeFrame=%s{tf.ToString()}"
        | League name -> $"league=%s{name}"

type FixtureFilter =
    | HeadToHead of int
with
    override this.ToString() =
        match this with
        | HeadToHead n -> $"head2head=%d{n}"

type Venue =
    | Home
    | Away
with
    override this.ToString() =
        match this with
        | Home -> "home"
        | Away -> "away"

type TeamFixturesFilter =
    | Season of int
    | TimeFrame of TimeFrame
    | Venue of Venue
with
    override this.ToString() =
        match this with
        | Season year -> $"season=%d{year}"
        | TimeFrame tf -> $"timeFrame=%s{tf.ToString()}"
        | Venue v -> $"venue=%s{v.ToString()}"

let private toQuery lst =
    match lst with
    | [] -> ""
    | xs -> String.Join("&", xs |> List.map (fun x -> x.ToString())) |> sprintf "?%s"

let private baseUri =
    Uri "https://api.football-data.org/"

let private createClient (authToken: string) =
    let client = new HttpClient()
    client.BaseAddress <- baseUri
    client.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
    client

let private apiCall<'T> (jsonOptions: JsonSerializerOptions) authToken (uri: string) (ct: CancellationToken) = task {
    use client = createClient authToken
    let! response = client.GetAsync(uri)
    if response.IsSuccessStatusCode then
        let! jsonStream = response.Content.ReadAsStreamAsync()
        let! result = JsonSerializer.DeserializeAsync<'T>(jsonStream, jsonOptions, ct)
        return Ok result
    else
        let! jsonStream = response.Content.ReadAsStreamAsync()
        let! error = JsonSerializer.DeserializeAsync<Error>(jsonStream, jsonOptions, ct)
        return Error(response.StatusCode, response.ReasonPhrase, error)
}

/// List all available competitions.
let getCompetitions (jsonOptions: JsonSerializerOptions) authToken (ct: CancellationToken) = task {
    let uri = $"/v4/competitions"
    let! comp = apiCall<CompetitionList> jsonOptions authToken uri ct
    return comp |> Result.map (fun x -> x.Competitions)
}

/// List all teams for a certain competition.
let getCompetitionTeams (jsonOptions: JsonSerializerOptions) authToken (competitionId: Id) (ct: CancellationToken) = task {
    let uri = $"/v4/competitions/%d{competitionId}/teams"
    return! apiCall<CompetitionTeams> jsonOptions authToken uri ct
}

/// List all fixtures for a certain competition.
let getCompetitionFixtures (jsonOptions: JsonSerializerOptions) authToken (competitionId: Id) (filters: CompetitionFixtureFilter list) (ct: CancellationToken) = task {
    let uri = $"/v4/competitions/%d{competitionId}/matches%s{filters |> toQuery}"
    return! apiCall<CompetitionFixtures> jsonOptions authToken uri ct
}

/// Show league table / current standing.
let getCompetitionLeagueTable (jsonOptions: JsonSerializerOptions) authToken (competitionId: Id) (ct: CancellationToken) = task {
    let uri = $"/v4/competitions/%d{competitionId}/standings"
    return! apiCall<CompetitionLeagueTable> jsonOptions authToken uri ct
}

[<CLIMutable>]
type CompetitionScorer = {
    Player: CompetitionPlayer
    Team: CompetitionTeam
    Goals: int
}

[<CLIMutable>]
type CompetitionScorers = {
    Scorers: CompetitionScorer array
}

let getScorers (jsonOptions: JsonSerializerOptions) authToken (competitionId: Id) (ct: CancellationToken) = task {
    let uri = $"/v4/competitions/%d{competitionId}/scorers/?limit=500"
    return! apiCall<CompetitionScorers> jsonOptions authToken uri ct
}

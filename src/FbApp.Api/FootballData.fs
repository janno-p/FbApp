[<RequireQualifiedAccess>]
module FbApp.Api.FootballData

open FbApp.Api.Serialization.Converters
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System
open System.Net.Http

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
    Crest: string
    }

[<CLIMutable>]
type GroupEntry =
    {
        Position: int
        Team: GroupEntryTeam
        PlayedGames: int
        Points: int
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
        CrestURI: string
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
type CompetitionTeam =
    {
        Id: int64
        Name: string
        [<JsonProperty("tla")>] Code: string
        ShortName: string
        // SquadMarketValue: string
        Crest: string
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

[<CLIMutable>]
type FixtureScore = {
    Home: int option
    Away: int option
    }

[<CLIMutable>]
type FixtureResult =
    {
        Winner: string option
        Duration: string
        FullTime: FixtureScore
        HalfTime: FixtureScore
        ExtraTime: FixtureScore
        Penalties: FixtureScore
    }

[<CLIMutable>]
type CompetitionFixtureTeam = {
    Id: int64 option
    Name: string option
    }

[<CLIMutable>]
type CompetitionFixture =
    {
        Id: int64
        [<JsonProperty("utcDate")>] Date: DateTimeOffset
        Status: string
        Matchday: int option
        HomeTeam: CompetitionFixtureTeam option
        AwayTeam: CompetitionFixtureTeam option
        [<JsonProperty("score")>] Result: FixtureResult option
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
        [<JsonProperty("matches")>] Fixtures: CompetitionFixture array
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
        [<JsonProperty("head2head")>] HeadToHead: HeadToHead
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

let serializer = JsonSerializer()
serializer.Converters.Add(OptionConverter())
serializer.ContractResolver <- CamelCasePropertyNamesContractResolver()

let deserialize<'T> (stream: IO.Stream) =
    use reader = new JsonTextReader(new IO.StreamReader(stream))
    serializer.Deserialize(reader, typeof<'T>) |> unbox<'T>

let private toQuery lst =
    match lst with
    | [] -> ""
    | xs -> String.Join("&", xs |> List.map (fun x -> x.ToString())) |> sprintf "?%s"

let private baseUri =
    Uri("http://api.football-data.org/v2/")

let private createClient (authToken: string) =
    let client = new HttpClient()
    client.BaseAddress <- baseUri
    client.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
    client

let private apiCall<'T> authToken (uri: string) = task {
    use client = createClient authToken
    let! response = client.GetAsync(uri)
    if response.IsSuccessStatusCode then
        let! jsonStream = response.Content.ReadAsStreamAsync()
        return Ok(deserialize<'T> jsonStream)
    else
        let! jsonStream = response.Content.ReadAsStreamAsync()
        return Error(response.StatusCode, response.ReasonPhrase, deserialize<Error> jsonStream)
}

/// List all available competitions.
let getCompetitions authToken = task {
    let uri = $"competitions/{2000L}"
    let! comp = apiCall<Competition> authToken uri
    return comp |> Result.map (fun x -> [| x |])
}

/// List all teams for a certain competition.
let getCompetitionTeams authToken (competitionId: Id) = task {
    let uri = $"/v4/competitions/%d{competitionId}/teams"
    return! apiCall<CompetitionTeams> authToken uri
}

/// List all fixtures for a certain competition.
let getCompetitionFixtures authToken (competitionId: Id) (filters: CompetitionFixtureFilter list) = task {
    let uri = $"/v4/competitions/%d{competitionId}/matches%s{filters |> toQuery}"
    return! apiCall<CompetitionFixtures> authToken uri
}

/// Show league table / current standing.
let getCompetitionLeagueTable authToken (competitionId: Id) = task {
    let uri = $"/v4/competitions/%d{competitionId}/standings"
    return! apiCall<CompetitionLeagueTable> authToken uri
}

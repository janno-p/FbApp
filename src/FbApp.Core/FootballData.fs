[<RequireQualifiedAccess>]
module FbApp.Core.FootballData

open FbApp.Core.Serialization.Converters
open FSharp.Control.Tasks
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System
open System.Collections.Generic
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
type GroupEntry =
    {
        Group: string
        Rank: int
        Team: string
        TeamId: int64
        PlayedGames: int
        CrestURI: string
        Points: int
        Goals: int
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
        [<JsonProperty("_links")>] Links: LeagueEntryLinks
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

type CompetitionLeagueTableStandings =
    | Groups of Dictionary<string, GroupEntry array>
    | League of LeagueEntry array

type private CompetitionLeagueTableConverter () =
    inherit JsonConverter ()
    let expectedType = typeof<CompetitionLeagueTableStandings>
    override __.CanConvert typ =
        expectedType.IsAssignableFrom(typ)
    override __.WriteJson (_,_,_) =
        raise (NotImplementedException())
    override __.ReadJson (reader, typ, _, serializer) =
        match reader.TokenType with
        | JsonToken.StartObject ->
            Groups (serializer.Deserialize<Dictionary<string, GroupEntry array>>(reader)) |> box
        | _ ->
            League (serializer.Deserialize<LeagueEntry array>(reader)) |> box

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
        // [<JsonProperty("_links")>] Links: CompetitionLinks
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
        [<JsonProperty("_links")>] Links: CompetitionTeamLinks
        Name: string
        Code: string
        ShortName: string
        SquadMarketValue: string
        CrestUrl: string
    }
with
    member this.Id =
        this.Links.Self |> parseIdSuffix

[<CLIMutable>]
type CompetitionTeams =
    {
        [<JsonProperty("_links")>] Links: CompetitionTeamsLinks
        Count: int
        Teams: CompetitionTeam array
    }

[<CLIMutable>]
type CompetitionLeagueTable =
    {
        LeagueCaption: string
        Matchday: int
        Standings: CompetitionLeagueTableStandings
    }

[<CLIMutable>]
type FixtureResultGoals =
    {
        GoalsHomeTeam: int
        GoalsAwayTeam: int
    }

[<CLIMutable>]
type FixtureResult =
    {
        GoalsHomeTeam: int option
        GoalsAwayTeam: int option
        HalfTime: FixtureResultGoals option
        ExtraTime: FixtureResultGoals option
        PenaltyShootout: FixtureResultGoals option
    }

[<CLIMutable>]
type CompetitionFixture =
    {
        [<JsonProperty("_links")>] Links: CompetitionFixtureLinks
        Date: DateTimeOffset
        Status: string
        Matchday: int
        HomeTeamName: string
        AwayTeamName: string
        Result: FixtureResult option
        //Odds
    }
with
    member this.Id =
        this.Links.Self |> parseIdSuffix
    member this.HomeTeamId =
        this.Links.HomeTeam |> parseIdSuffix
    member this.AwayTeamId =
        this.Links.AwayTeam |> parseIdSuffix

[<CLIMutable>]
type CompetitionFixtures =
    {
        [<JsonProperty("_links")>] Links: CompetitionFixturesLinks
        Count: int
        Fixtures: CompetitionFixture array
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
        [<JsonProperty("_links")>] Links: TeamFixturesLinks
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
        [<JsonProperty("_links")>] Links: TeamPlayersLinks
        Count: int
        Players: TeamPlayer array
    }

type LeagueTableFilter =
    | Matchday of int
with
    override this.ToString() =
        match this with
        | Matchday day -> sprintf "matchday=%d" day

type TimeFrame =
    | Previous of int
    | Next of int
with
    override this.ToString() =
        match this with
        | Previous v -> sprintf "p%d" v
        | Next v -> sprintf "n%d" v

type CompetitionFixtureFilter =
    | TimeFrame of TimeFrame
    | TimeFrameRange of DateTimeOffset * DateTimeOffset
    | Matchday of int
with
    override this.ToString() =
        match this with
        | TimeFrame tf -> sprintf "timeFrame=%s" (tf.ToString())
        | TimeFrameRange (s, e) -> sprintf "timeFrameStart=%s&timeFrameEnd=%s" (s.ToString("yyyy-MM-dd")) (e.ToString("yyyy-MM-dd"))
        | Matchday n -> sprintf "matchday=%d" n

type FixturesFilter =
    | TimeFrame of TimeFrame
    | League of string
with
    override this.ToString() =
        match this with
        | TimeFrame tf -> sprintf "timeFrame=%s" (tf.ToString())
        | League name -> sprintf "league=%s" name

type FixtureFilter =
    | HeadToHead of int
with
    override this.ToString() =
        match this with
        | HeadToHead n -> sprintf "head2head=%d" n

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
        | Season year -> sprintf "season=%d" year
        | TimeFrame tf -> sprintf "timeFrame=%s" (tf.ToString())
        | Venue v -> sprintf "venue=%s" (v.ToString())

let serializer = JsonSerializer()
serializer.Converters.Add(OptionConverter())
serializer.Converters.Add(CompetitionLeagueTableConverter())
serializer.ContractResolver <- CamelCasePropertyNamesContractResolver()

let deserialize<'T> (stream: IO.Stream) =
    use reader = new JsonTextReader(new IO.StreamReader(stream))
    serializer.Deserialize(reader, typeof<'T>) |> unbox<'T>

let private toQuery lst =
    match lst with
    | [] -> ""
    | xs -> String.Join("&", xs |> List.map (fun x -> x.ToString())) |> sprintf "?%s"

let private baseUri =
    Uri("http://api.football-data.org/v1/")

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

/// List all teams for a certain competition.
let getCompetitionTeams authToken (competitionId: Id) = task {
    let uri = sprintf "competitions/%d/teams" competitionId
    return! apiCall<CompetitionTeams> authToken uri
}

/// Show league table / current standing.
let getCompetitionLeagueTable authToken (competitionId: Id) (filters: LeagueTableFilter list) = task {
    let uri = sprintf "competitions/%d/leagueTable%s" competitionId (filters |> toQuery)
    return! apiCall<CompetitionLeagueTable> authToken uri
}

/// List all fixtures for a certain competition.
let getCompetitionFixtures authToken (competitionId: Id) (filters: CompetitionFixtureFilter list) = task {
    let uri = sprintf "competitions/%d/fixtures%s" competitionId (filters |> toQuery)
    return! apiCall<CompetitionFixtures> authToken uri
}

/// List fixtures across a set of competitions.
let getFixtures authToken (filters: FixturesFilter list) = task {
    let uri = sprintf "fixtures%s" (filters |> toQuery)
    return! apiCall<Fixtures> authToken uri
}

/// Show one fixture.
let getFixture authToken (fixtureId: Id) (filters: FixtureFilter list) = task {
    let uri = sprintf "fixtures/%d%s" fixtureId (filters |> toQuery)
    return! apiCall<Fixture> authToken uri
}

/// Show all fixtures for a certain team.
let getTeamFixtures authToken (teamId: Id) (filters: TeamFixturesFilter list) = task {
    let uri = sprintf "teams/%d/fixtures%s" teamId (filters |> toQuery)
    return! apiCall<TeamFixtures> authToken uri
}

/// Show one team.
let getTeam authToken (teamId: Id) = task {
    let uri = sprintf "teams/%d" teamId
    return! apiCall<CompetitionTeam> authToken uri
}

/// Show all players for a certain team.
let getTeamPlayers authToken (teamId: Id) = task {
    let uri = sprintf "teams/%d/players" teamId
    return! apiCall<TeamPlayers> authToken uri
}

module Api2 =
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
    type CompetitionMatchFilter =
        | DateRange of DateTimeOffset * DateTimeOffset
        | Stage of string
        | Status of string
        | Matchday of int
        | Group of string
    with
        override this.ToString() =
            match this with
            | DateRange (dateFrom, dateTo) -> sprintf "dateFrom=%s&dateTo=%s" (dateFrom.ToString("yyyy-MM-dd")) (dateTo.ToString("yyyy-MM-dd"))
            | Stage stage -> sprintf "stage=%s" stage
            | Status status -> sprintf "status=%s" status
            | Matchday matchday -> sprintf "matchday=%d" matchday
            | Group group -> sprintf "group=%s" group

    [<CLIMutable>]
    type CompetitionSeason =
        {
            Id: Id
            StartDate: DateTimeOffset
            EndDate: DateTimeOffset
            CurrentMatchday: int
        }

    [<CLIMutable>]
    type Resource =
        {
            Id: Id
            Name: string
        }

    [<CLIMutable>]
    type CompetitionMatchScoreGoals =
        {
            HomeTeam: int option
            AwayTeam: int option
        }

    [<CLIMutable>]
    type CompetitionMatchScore =
        {
            Winner: string option
            Duration: string
            FullTime: CompetitionMatchScoreGoals
            HalfTime: CompetitionMatchScoreGoals
            ExtraTime: CompetitionMatchScoreGoals
            Penalties: CompetitionMatchScoreGoals
        }

    [<CLIMutable>]
    type CompetitionMatchReferee =
        {
            Id: Id
            Name: string
            Nationality: string
        }

    [<CLIMutable>]
    type CompetitionMatch =
        {
            Id: Id
            Competition: Resource
            Season: CompetitionSeason
            UtcDate: DateTimeOffset
            Status: string
            Matchday: int option
            Stage: string
            Group: string
            LastUpdated: DateTimeOffset
            HomeTeam: Resource
            AwayTeam: Resource
            Score: CompetitionMatchScore
            Referees: CompetitionMatchReferee array
        }

    [<CLIMutable>]
    type CompetitionMatches =
        {
            Count: int
            Season: CompetitionSeason
            Matches: CompetitionMatch array
            // filters
        }

    let getCompetitionMatches authToken (competitionId: Id) (filters: CompetitionMatchFilter list) = task {
        let uri = sprintf "competitions/%d/matches%s" competitionId (filters |> toQuery)
        return! apiCall<CompetitionMatches> authToken uri
    }

    /// List all available competitions.
    let getCompetitions authToken = task {
        let uri = sprintf "competitions?plan=TIER_ONE"
        let! comp = apiCall<Competition> authToken uri
        return comp |> Result.map (fun x -> [| x |])
    }

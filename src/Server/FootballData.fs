[<RequireQualifiedAccess>]
module FbApp.Server.FootballData

open Giraffe
open Newtonsoft.Json
open System
open System.Net.Http

let mutable footballDataToken = ""

[<CLIMutable>]
type Link = { Href: string }

[<CLIMutable>]
type CompetitionTeamLinks = { Self: Link }

[<CLIMutable>]
type CompetitionFixtureLinks =
    {
        Self: Link
        HomeTeam: Link
        AwayTeam: Link
    }

[<CLIMutable>]
type CompetitionTeamData =
    {
        [<JsonProperty("_links")>]
        Links: CompetitionTeamLinks
        Name: string
        Code: string
        CrestUrl: string
    }

[<CLIMutable>]
type CompetitionTeamsData = { Teams: CompetitionTeamData[] }

[<CLIMutable>]
type CompetitionFixtureData =
    {
        [<JsonProperty("_links")>] Links: CompetitionFixtureLinks
        Date: DateTime
    }

[<CLIMutable>]
type CompetitionFixturesData = { Fixtures: CompetitionFixtureData[] }

[<CLIMutable>]
type CompetitionData =
    {
        Id: int
        Caption: string
        League: string
        Year: string
        CurrentMatchday: int
        NumberOfMatchdays: int
        NumberOfTeams: int
        NumberOfGames: int
        LastUpdated: DateTime
    }

let private baseUri =
    Uri("http://api.football-data.org/v1/")

let private downloadString (uri: string) = task {
    use client = new HttpClient()
    client.BaseAddress <- baseUri
    client.DefaultRequestHeaders.Add("X-Auth-Token", footballDataToken)
    return! client.GetStringAsync(uri)
}

let private downloadData<'T> (uri: string) = task {
    let! json = downloadString uri
    return JsonConvert.DeserializeObject<'T> json
}

let loadCompetitionsOf year = task {
    return! downloadData<CompetitionData[]> (sprintf "competitions?season=%d" year)
}

let private parseLinkId (link: Link) =
    let href = link.Href
    let index = href.LastIndexOf('/')
    Convert.ToInt64(href.Substring(index + 1))

let loadCompetitionTeams (id: int64) = task {
    let! teams = downloadData<CompetitionTeamsData> (sprintf "competitions/%d/teams" id)
    return teams.Teams |> Array.map (fun x ->
        let team: Competition.TeamAssignment =
            {
                Name = x.Name
                Code = x.Code
                FlagUrl = x.CrestUrl
                ExternalId = parseLinkId x.Links.Self
            }
        team
    )
}

let loadCompetitionFixtures (id: int64) = task {
    let! fixtures = downloadData<CompetitionFixturesData> (sprintf "competitions/%d/fixtures" id)
    return fixtures.Fixtures |> Array.map (fun x ->
        let fixture: Competition.FixtureAssignment =
            {
                HomeTeamId = parseLinkId x.Links.HomeTeam
                AwayTeamId = parseLinkId x.Links.AwayTeam
                Date = x.Date
                ExternalId = parseLinkId x.Links.Self
            }
        fixture
    )
}

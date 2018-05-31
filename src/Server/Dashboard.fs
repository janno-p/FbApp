module FbApp.Server.Dashboard

open Giraffe
open Newtonsoft.Json
open Saturn
open System
open System.Net.Http

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

type CompetitionItem =
    {
        Label: string
        Value: int
    }

type CompetitionDto =
    {
        Description: string
        ExternalSource: int
    }

let getCompetitionSources year: HttpHandler =
    (fun next context ->
        task {
            use client = new HttpClient()
            let! json = client.GetStringAsync(sprintf "https://www.football-data.org/v1/competitions?season=%d" year)
            let competitions =
                JsonConvert.DeserializeObject<CompetitionData[]>(json)
                |> Array.map (fun x -> { Label = sprintf "%s (%s)" x.Caption x.League; Value = x.Id })
            return! Successful.OK competitions next context
        })

let addCompetition: HttpHandler =
    (fun next context ->
        task {
            let competitionDto = context.BindJsonAsync<CompetitionDto>()
            return! Successful.OK () next context
        })

let dashboardScope = scope {
    getf "/competition_sources/%i" getCompetitionSources
}

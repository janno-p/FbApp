module FbApp.Server.Predict

open FbApp.Server.Projection
open Giraffe
open MongoDB.Driver
open Saturn
open System
open System.Collections.Generic

[<CLIMutable>]
type TeamDto =
    {
        Name: string
        FlagUrl: string
    }

[<CLIMutable>]
type FixtureDto =
    {
        Id: int64
        HomeTeamId: int64
        AwayTeamId: int64
    }

[<CLIMutable>]
type FixturesDto =
    {
        CompetitionId: Guid
        Teams: IDictionary<int64, TeamDto>
        Fixtures: FixtureDto[]
        Groups: IDictionary<string, int64[]>
    }

let private getFixtures: HttpHandler =
    (fun next context -> task {
        let f = Builders<Projections.Competition>.Filter.Eq((fun x -> x.ExternalSource), 467L)
        let! activeCompetition = competitions.Find(f).Limit(Nullable(1)).SingleAsync()
        let fixtures =
            {
                CompetitionId =
                    activeCompetition.Id
                Teams =
                    activeCompetition.Teams
                    |> Array.map (fun x -> (x.ExternalId, { Name = x.Name; FlagUrl = x.FlagUrl }))
                    |> dict
                Fixtures =
                    activeCompetition.Fixtures
                    |> Array.map (fun x -> { Id = x.ExternalId; HomeTeamId = x.HomeTeamId; AwayTeamId = x.AwayTeamId })
                Groups =
                    activeCompetition.Groups
            }
        return! Successful.OK fixtures next context
    })

let predictScope = scope {
    get "/fixtures" getFixtures
}

module FbApp.Server.Fixtures

open FbApp.Server.Repositories
open Giraffe
open Saturn
open System

[<CLIMutable>]
type TeamDto =
    {
        Name: string
        FlagUrl: string
    }
with
    static member FromProjection(team: ReadModels.Team) =
        { Name = team.Name; FlagUrl = team.FlagUrl }

[<CLIMutable>]
type FixturePredictionDto =
    {
        Name: string
        Result: string
    }
with
    static member FromProjection(prediction: ReadModels.FixturePrediction) =
        let fixName (name: string) =
            name.Split([|' '|], 2).[0]
        { Name = fixName prediction.Name; Result = prediction.Result }

[<CLIMutable>]
type FixtureDto =
    {
        Id: Guid
        Date: DateTimeOffset
        PreviousFixtureId: Nullable<Guid>
        NextFixtureId: Nullable<Guid>
        HomeTeam: TeamDto
        AwayTeam: TeamDto
        Status: string
        HomeGoals: Nullable<int>
        AwayGoals: Nullable<int>
        Predictions: FixturePredictionDto[]
    }
with
    static member FromProjection (fixture: ReadModels.Fixture) =
        {
            Id = fixture.Id
            Date = fixture.Date.ToLocalTime()
            PreviousFixtureId = fixture.PreviousId
            NextFixtureId = fixture.NextId
            HomeTeam = TeamDto.FromProjection(fixture.HomeTeam)
            AwayTeam = TeamDto.FromProjection(fixture.AwayTeam)
            Status = fixture.Status
            HomeGoals = fixture.HomeGoals
            AwayGoals = fixture.AwayGoals
            Predictions = fixture.Predictions |> Array.map FixturePredictionDto.FromProjection |> Array.sortBy (fun u -> u.Name.ToLowerInvariant())
        }

let getFixture (id: Guid) : HttpHandler =
    (fun next ctx -> task {
        let! fixture = Fixtures.get id
        let dto = FixtureDto.FromProjection(fixture)
        return! Successful.OK dto next ctx
    })

[<CLIMutable>]
type FixtureStatusDto =
    {
        Status: string
        HomeGoals: Nullable<int>
        AwayGoals: Nullable<int>
    }

let getFixtureStatus (id: Guid) : HttpHandler =
    (fun next ctx -> task {
        let! dto = Fixtures.getFixtureStatus id
        return! Successful.OK dto next ctx
    })

let getTimelyFixture : HttpHandler =
    (fun next ctx -> task {
        let! fixture = Fixtures.getTimelyFixture ()
        let dto = FixtureDto.FromProjection(fixture)
        return! Successful.OK dto next ctx
    })

let scope = scope {
    getf "/%O" getFixture
    getf "/%O/status" getFixtureStatus
    get "/timely" getTimelyFixture
}

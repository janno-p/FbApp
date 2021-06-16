module FbApp.Server.Fixtures

open FbApp.Server.Repositories
open FSharp.Control.Tasks
open Giraffe
open Saturn.Endpoint
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

let private fixName (name: string) =
    name.Split([|' '|], 2).[0]

[<CLIMutable>]
type FixturePredictionDto =
    {
        Name: string
        Result: string
    }
with
    static member FromProjection(prediction: ReadModels.FixtureResultPrediction) =
        { Name = fixName prediction.Name; Result = prediction.Result }

[<CLIMutable>]
type QualifierPredictionDto =
    {
        Name: string
        HomeQualifies: bool
        AwayQualifies: bool
    }
with
    static member FromProjection(prediction: ReadModels.QualificationPrediction) =
        { Name = fixName prediction.Name; HomeQualifies = prediction.HomeQualifies; AwayQualifies = prediction.AwayQualifies }

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
        Stage: string
        FullTime: int array
        ExtraTime: int array
        Penalties: int array
        ResultPredictions: FixturePredictionDto array
        QualifierPredictions: QualifierPredictionDto array
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
            Stage = fixture.Stage
            FullTime = fixture.FullTime
            ExtraTime = fixture.ExtraTime
            Penalties = fixture.Penalties
            ResultPredictions = fixture.ResultPredictions |> Array.map FixturePredictionDto.FromProjection |> Array.sortBy (fun u -> u.Name.ToLowerInvariant())
            QualifierPredictions = fixture.QualificationPredictions |> Array.map QualifierPredictionDto.FromProjection |> Array.sortBy (fun u -> u.Name.ToLowerInvariant())
        }

let getFixture (id: Guid) : HttpHandler =
    (fun next ctx -> task {
        let! fixture = Fixtures.get id
        let dto = FixtureDto.FromProjection(fixture)
        return! Successful.OK dto next ctx
    })

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

let scope = router {
    getf "/%O" getFixture
    getf "/%O/status" getFixtureStatus
    get "/timely" getTimelyFixture
}

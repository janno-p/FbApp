module FbApp.Fixtures.Dto

open System
open FbApp.Common
open FbApp.Fixtures.Queries


type TeamDto = {
    Name: string
    FlagUrl: string
}


type FixturePredictionDto = {
    Name: string
    Result: string
}


type QualifierPredictionDto = {
    Name: string
    HomeQualifies: bool
    AwayQualifies: bool
}


type FixtureDto = {
    Id: Guid
    Date: int64
    PreviousFixtureId: Guid Nullable
    NextFixtureId: Guid Nullable
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


module TeamDto =

    let fromTeam (team: Team) =
        {
            Name = team.Name
            FlagUrl = team.FlagUrl
        }


module FixturePredictionDto =

    let fromFixtureResultPrediction (prediction: FixtureResultPrediction) =
        {
            Name = Helpers.excludeLastName prediction.Name
            Result = prediction.Result
        }


module QualifierPredictionDto =

    let fromQualificationPrediction (prediction: QualificationPrediction) =
        {
            Name = Helpers.excludeLastName prediction.Name
            HomeQualifies = prediction.HomeQualifies
            AwayQualifies = prediction.AwayQualifies
        }


module FixtureDto =

    let fromFixture (fixture: Fixture) =
        {
            Id = fixture.Id
            Date = fixture.Date.ToUnixTimeMilliseconds()
            PreviousFixtureId = fixture.PreviousId
            NextFixtureId = fixture.NextId
            HomeTeam = TeamDto.fromTeam fixture.HomeTeam
            AwayTeam = TeamDto.fromTeam fixture.AwayTeam
            Status = fixture.Status
            Stage = fixture.Stage
            FullTime =
                match fixture.FullTime, fixture.Penalties with
                | [| fth; fta |], [| pth; pta |] -> [| fth - pth; fta - pta |]
                | _ -> fixture.FullTime
            ExtraTime = fixture.ExtraTime
            Penalties = fixture.Penalties
            ResultPredictions =
                fixture.ResultPredictions
                    |> Array.map FixturePredictionDto.fromFixtureResultPrediction
                    |> Array.sortBy (fun u -> u.Name.ToLowerInvariant())
            QualifierPredictions =
                fixture.QualificationPredictions
                    |> Array.map QualifierPredictionDto.fromQualificationPrediction
                    |> Array.sortBy (fun u -> u.Name.ToLowerInvariant())
        }

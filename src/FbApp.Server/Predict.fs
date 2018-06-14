module FbApp.Server.Predict

open FbApp.Core.Aggregate
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

[<CLIMutable>]
type PredictionFixtureDto =
    {
        Fixture: int64
        HomeTeam: int64
        AwayTeam: int64
        Result: string
    }

[<CLIMutable>]
type PredictionDto =
    {
        CompetitionId: Guid
        Teams: IDictionary<int64, TeamDto>
        Fixtures: PredictionFixtureDto[]
        RoundOf16: int64[]
        RoundOf8: int64[]
        RoundOf4: int64[]
        RoundOf2: int64[]
        Winner: int64
    }

let private getActiveCompetition () = task {
    let f = Builders<Projections.Competition>.Filter.Eq((fun x -> x.ExternalSource), 467L)
    return! competitions.Find(f).Limit(Nullable(1)).SingleAsync()
}

let private mapTeams (competition: Projections.Competition) =
    competition.Teams
    |> Array.map (fun x -> (x.ExternalId, { Name = x.Name; FlagUrl = x.FlagUrl }))
    |> dict

let private getFixtures: HttpHandler =
    (fun next context -> task {
        let! activeCompetition = getActiveCompetition ()
        let fixtures =
            {
                CompetitionId =
                    activeCompetition.Id
                Teams =
                    (activeCompetition |> mapTeams)
                Fixtures =
                    activeCompetition.Fixtures
                    |> Array.map (fun x -> { Id = x.ExternalId; HomeTeamId = x.HomeTeamId; AwayTeamId = x.AwayTeamId })
                Groups =
                    activeCompetition.Groups
            }
        return! Successful.OK fixtures next context
    })

let private savePredictions: HttpHandler =
    (fun next context -> task {
        let! dto = context.BindJsonAsync<Prediction.PredictionRegistrationInput>()
        let user = Auth.createUser context.User context
        let command = Prediction.Register (dto, user.Name, user.Email)
        let id = Prediction.Id (dto.CompetitionId, Prediction.Email user.Email)
        let! result = Aggregate.Handlers.predictionHandler (id, Some(0L)) command
        match result with
        | Ok(_) -> return! Successful.ACCEPTED id next context
        | Error(WrongExpectedVersion) -> return! RequestErrors.CONFLICT "Prediction already exists" next context
        | Error(e) -> return! RequestErrors.BAD_REQUEST e next context
    })

let private getCurrentPrediction: HttpHandler =
    (fun next context -> task {
        let! activeCompetition = getActiveCompetition ()
        let user = Auth.createUser context.User context
        let predictionId = Prediction.Id (activeCompetition.Id, Prediction.Email user.Email) |> Prediction.streamId
        let f = Builders<Projections.Prediction>.Filter.Eq((fun x -> x.Id), predictionId)
        let! prediction = predictions.Find(f) |> FindFluent.trySingleAsync
        match prediction with
        | Some(prediction) ->
            let mapFixture (fixture: Projections.FixtureResult) : PredictionFixtureDto =
                let x = activeCompetition.Fixtures |> Array.find (fun n -> n.ExternalId = fixture.FixtureId)
                {
                    Fixture = fixture.FixtureId
                    HomeTeam = x.HomeTeamId
                    AwayTeam = x.AwayTeamId
                    Result = fixture.Result
                }
            let dto: PredictionDto =
                {
                    CompetitionId = activeCompetition.Id
                    Teams = (activeCompetition |> mapTeams)
                    Fixtures = prediction.Fixtures |> Array.map mapFixture
                    RoundOf16 = prediction.QualifiersRoundOf16
                    RoundOf8 = prediction.QualifiersRoundOf8
                    RoundOf4 = prediction.QualifiersRoundOf4
                    RoundOf2 = prediction.QualifiersRoundOf2
                    Winner = prediction.Winner
                }
            return! Successful.OK dto next context
        | None -> return! RequestErrors.NOT_FOUND "Prediction does not exist" next context
    })

let predictScope = scope {
    post "/" (Auth.authPipe >=> Auth.validateXsrfToken >=> savePredictions)
    get "/fixtures" getFixtures
    get "/current" (Auth.authPipe >=> Auth.validateXsrfToken >=> getCurrentPrediction)
}

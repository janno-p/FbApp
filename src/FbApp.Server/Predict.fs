module FbApp.Server.Predict

open FbApp.Core
open FbApp.Domain
open FbApp.Server.Repositories
open FSharp.Control.Tasks
open Giraffe
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

[<CLIMutable>]
type FixturePredictionDto =
    {
        Name: string
        Result: string
    }

let private mapTeams (competition: ReadModels.Competition) =
    competition.Teams
    |> Array.map (fun x -> (x.ExternalId, { Name = x.Name; FlagUrl = x.FlagUrl }))
    |> dict

let private getFixtures: HttpHandler =
    (fun next context -> task {
        let! activeCompetition = Competitions.getActive ()
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
        let! dto = context.BindJsonAsync<Predictions.PredictionRegistrationInput>()
        let user = Auth.createUser context.User context
        let! competition = Competitions.get dto.CompetitionId
        match competition with
        | Some(competition) when competition.Date > DateTimeOffset.Now ->
            let command = Predictions.Register (dto, user.Name, user.Email)
            let id = Predictions.createId (dto.CompetitionId, Predictions.Email user.Email)
            let! result = CommandHandlers.predictionsHandler (id, Aggregate.New) command
            match result with
            | Ok(_) -> return! Successful.ACCEPTED id next context
            | Error(Aggregate.WrongExpectedVersion) -> return! RequestErrors.CONFLICT "Prediction already exists" next context
            | Error(e) -> return! RequestErrors.BAD_REQUEST e next context
        | Some(_) ->
            return! RequestErrors.BAD_REQUEST "Competition has already begun" next context
        | None ->
            return! RequestErrors.BAD_REQUEST "Invalid competition id" next context
    })

let private getCurrentPrediction: HttpHandler =
    (fun next context -> task {
        let! activeCompetition = Competitions.getActive ()
        let user = Auth.createUser context.User context
        let! prediction = Predictions.get (activeCompetition.Id, user.Email)
        match prediction with
        | Some(prediction) ->
            let mapFixture (fixture: ReadModels.PredictionFixtureResult) : PredictionFixtureDto =
                let x = activeCompetition.Fixtures |> Array.find (fun n -> n.ExternalId = fixture.FixtureId)
                {
                    Fixture = fixture.FixtureId
                    HomeTeam = x.HomeTeamId
                    AwayTeam = x.AwayTeamId
                    Result = fixture.PredictedResult
                }
            let dto: PredictionDto =
                {
                    CompetitionId = activeCompetition.Id
                    Teams = (activeCompetition |> mapTeams)
                    Fixtures = prediction.Fixtures |> Array.map mapFixture
                    RoundOf16 = prediction.QualifiersRoundOf16 |> Array.map (fun x -> x.Id)
                    RoundOf8 = prediction.QualifiersRoundOf8 |> Array.map (fun x -> x.Id)
                    RoundOf4 = prediction.QualifiersRoundOf4 |> Array.map (fun x -> x.Id)
                    RoundOf2 = prediction.QualifiersRoundOf2 |> Array.map (fun x -> x.Id)
                    Winner = prediction.Winner.Id
                }
            return! Successful.OK dto next context
        | None -> return! RequestErrors.NOT_FOUND "Prediction does not exist" next context
    })

let getCompetitionStatus () = task {
    let! competition = Competitions.getActive ()
    return if competition.Date < DateTimeOffset.Now then "competition-running" else "accept-predictions"
}

let predictScope = router {
    post "/" (Auth.authPipe >=> Auth.validateXsrfToken >=> savePredictions)
    get "/fixtures" getFixtures
    get "/current" (Auth.authPipe >=> Auth.validateXsrfToken >=> getCurrentPrediction)
}

module FbApp.Api.Predict

open FbApp.Api
open FbApp.Api.Domain
open FbApp.Api.Repositories
open Giraffe
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Saturn
open Saturn.Endpoint
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
        match! Competitions.tryGetActive (context.RequestServices.GetRequiredService<IMongoDatabase>()) with
        | Some activeCompetition ->
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
        | None ->
            return! RequestErrors.NOT_FOUND "No active competition" next context
    })



let private savePredictions: Auth.AuthHttpHandler =
    (fun user next context -> task {
        let! dto = context.BindJsonAsync<Predictions.PredictionRegistrationInput>()
        let! competition = Competitions.get (context.RequestServices.GetRequiredService<IMongoDatabase>()) dto.CompetitionId
        match competition with
        | Some(competition) when competition.Date > DateTimeOffset.Now ->
            let command = Predictions.Register (dto, user.Name, user.Email)
            let id = Predictions.createId (dto.CompetitionId, Predictions.Email user.Email)
            let! result = CommandHandlers.predictionsHandler (id, Aggregate.New) command
            match result with
            | Ok _ -> return! Successful.ACCEPTED id next context
            | Error(Aggregate.WrongExpectedVersion) -> return! RequestErrors.CONFLICT "Prediction already exists" next context
            | Error(e) -> return! RequestErrors.BAD_REQUEST e next context
        | Some _ ->
            return! RequestErrors.BAD_REQUEST "Competition has already begun" next context
        | None ->
            return! RequestErrors.BAD_REQUEST "Invalid competition id" next context
    })

let private getCurrentPrediction: Auth.AuthHttpHandler =
    (fun user next context -> task {
        let db = context.RequestServices.GetRequiredService<IMongoDatabase>()
        match! Competitions.tryGetActive db with
        | Some activeCompetition ->
            let! prediction = Predictions.get db (activeCompetition.Id, user.Email)
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
        | None ->
            return! RequestErrors.NOT_FOUND "No active competition" next context
    })

let getCompetitionStatus db = task {
    match! Competitions.tryGetActive db with
    | Some competition ->
        return if competition.Date < DateTimeOffset.Now then "competition-running" else "accept-predictions"
    | None ->
        return "no-active-competition"
}

let predictScope = router {
    post "/" (Auth.authPipe >=> (Auth.withUser savePredictions))
    get "/fixtures" getFixtures
    get "/current" (Auth.authPipe >=> (Auth.withUser getCurrentPrediction))
}

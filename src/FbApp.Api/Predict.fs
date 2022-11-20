module FbApp.Api.Predict

open DnsClient.Internal
open FbApp.Api
open FbApp.Api.Domain
open FbApp.Api.Repositories
open FbApp.Api.Repositories.ReadModels
open Giraffe
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Saturn
open Saturn.Endpoint
open System

[<CLIMutable>]
type TeamDto =
    {
        Id: int64
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
type GroupDto =
    {
        Name: string
        TeamIds: int64[]
    }

[<CLIMutable>]
type PlayerDto =
    {
        Id: int64
        Name: string
        Position: string
        TeamId: int64
    }

[<CLIMutable>]
type FixturesDto =
    {
        CompetitionId: Guid
        Teams: TeamDto[]
        Fixtures: FixtureDto[]
        Groups: GroupDto[]
        Players: PlayerDto[]
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
        Teams: TeamDto[]
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
    |> Array.map (fun x -> { Id = x.ExternalId; Name = x.Name; FlagUrl = x.FlagUrl })

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
                        activeCompetition.Groups |> Seq.map (fun x -> { Name = x.Key; TeamIds = x.Value }) |> Seq.toArray
                    Players =
                        activeCompetition.Players |> Seq.map (fun x -> { Id = x.ExternalId; Name = x.Name; Position = x.Position; TeamId = x.TeamExternalId }) |> Seq.toArray
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

let logx : HttpHandler =
    fun next ctx -> task {
        let! dto = ctx.BindJsonAsync<Predictions.PredictionRegistrationInput>()
        let log = ctx.GetLogger<Predictions.PredictionRegistrationInput>()
        log.LogWarning("{Dto}", dto)
        return! next ctx
        }

let predictScope = router {
    post "/" (logx >=> Auth.mustBeLoggedIn >=> (Auth.withUser savePredictions))
    get "/fixtures" getFixtures
}

module FbApp.Api.Predict

open FbApp.Api
open FbApp.Api.Domain
open FbApp.Api.Repositories
open FbApp.Api.Repositories.ReadModels
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.Extensions.DependencyInjection
open MongoDB.Driver
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


let private mapTeams (competition: Competition) =
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


let predictScope: Endpoint list = [
    POST [
        route "/" (Auth.mustBeLoggedIn >=> (Auth.withUser savePredictions))
    ]
    GET [
        route "/fixtures" getFixtures
    ]
]

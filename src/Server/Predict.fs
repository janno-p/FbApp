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

let private savePredictions: HttpHandler =
    (fun next context -> task {
        let! dto = context.BindJsonAsync<Prediction.PredictionRegistrationInput>()
        let user = Auth.createUser context.User context
        let command = Prediction.Register (dto, user.Name, user.Email)
        let id = Prediction.Id (dto.CompetitionId, Prediction.Email user.Email)
        let! result = Aggregate.Handlers.predictionHandler (id, Some(0L)) command
        match result with
        | Ok(_) -> return! Successful.ACCEPTED id next context
        | Error(_) -> return! RequestErrors.BAD_REQUEST "" next context
    })

let predictScope = scope {
    post "/" (Auth.authPipe >=> Auth.validateXsrfToken >=> savePredictions)
    get "/fixtures" getFixtures
}

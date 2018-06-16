module FbApp.Server.Predict

open FbApp.Core
open FbApp.Domain
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

[<CLIMutable>]
type FixtureDetailsDto =
    {
        Date: DateTimeOffset
        HomeTeam: TeamDto
        AwayTeam: TeamDto
        Status: string
        HomeGoals: Nullable<int>
        AwayGoals: Nullable<int>
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
        let! dto = context.BindJsonAsync<Predictions.PredictionRegistrationInput>()
        let user = Auth.createUser context.User context
        let! competition = getCompetition dto.CompetitionId
        match competition with
        | Some(competition) when competition.Date > DateTimeOffset.Now ->
            let command = Predictions.Register (dto, user.Name, user.Email)
            let id = Predictions.Id (dto.CompetitionId, Predictions.Email user.Email)
            let! result = CommandHandlers.predictionsHandler (id, Aggregate.New) command
            match result with
            | Ok(_) -> return! Successful.ACCEPTED (id |> Predictions.streamId |> Guid.toString) next context
            | Error(Aggregate.WrongExpectedVersion) -> return! RequestErrors.CONFLICT "Prediction already exists" next context
            | Error(e) -> return! RequestErrors.BAD_REQUEST e next context
        | Some(_) ->
            return! RequestErrors.BAD_REQUEST "Competition has already begun" next context
        | None ->
            return! RequestErrors.BAD_REQUEST "Invalid competition id" next context
    })

let private getCurrentPrediction: HttpHandler =
    (fun next context -> task {
        let! activeCompetition = getActiveCompetition ()
        let user = Auth.createUser context.User context
        let predictionId = Predictions.Id (activeCompetition.Id, Predictions.Email user.Email) |> Predictions.streamId
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

let private getCompetitionStatus : HttpHandler =
    (fun next context -> task {
        let! competition = getActiveCompetition ()
        let status = if competition.Date < DateTimeOffset.Now then "competition-running" else "accept-predictions"
        return! Successful.OK status next context
    })

open MongoDB.Bson

let private getTimelyFixtures : HttpHandler =
    (fun next context -> task {
        let pipelines =
            PipelineDefinition<_,Projections.Fixture>.Create(
                (sprintf """{
                    $addFields: {
                        rank: {
                            $let: {
                                vars: { diff: { $abs: { $subtract: [ %d, { $arrayElemAt: [ "$Date", 0 ] } ] } } },
                                in: { $cond: { if: { $eq: [ "$Status", "IN_PLAY" ] }, then: 0, else: { $multiply: [ 1, "$$diff" ] } } }
                            }
                        }
                    }
                }""" System.DateTimeOffset.UtcNow.Ticks),
                """{ $sort: { rank: 1 } }""",
                """{ $limit: 2 }""",
                """{ $addFields: { rank: "$$REMOVE" } }"""
            )
        let! timelyFixtures = fixtures.Aggregate(pipelines).ToListAsync()
        if timelyFixtures.Count > 1 && timelyFixtures.[0].Date <> timelyFixtures.[1].Date then
            timelyFixtures.RemoveAt(1)
        let result =
            let mapTeam (t: Projections.Team) =
                { Name = t.Name; FlagUrl = t.FlagUrl }
            timelyFixtures
            |> Seq.map
                (fun x ->
                    {
                        Date = x.Date.ToLocalTime()
                        HomeTeam = mapTeam x.HomeTeam
                        AwayTeam = mapTeam x.AwayTeam
                        Status = x.Status
                        HomeGoals = x.HomeGoals
                        AwayGoals = x.AwayGoals
                    } : FixtureDetailsDto)
            |> Seq.toArray
        return! Successful.OK result next context
    })

let predictScope = scope {
    post "/" (Auth.authPipe >=> Auth.validateXsrfToken >=> savePredictions)
    get "/fixtures" getFixtures
    get "/current" (Auth.authPipe >=> Auth.validateXsrfToken >=> getCurrentPrediction)
    get "/status" getCompetitionStatus
    get "/timely" getTimelyFixtures
}

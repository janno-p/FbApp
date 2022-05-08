namespace FbApp.Web

open FbApp.Web.Repositories
open Giraffe
open MongoDB.Driver
open Saturn
open XploRe.Util

[<AutoOpen>]
module Extensions =
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.DependencyInjection

    type HttpContext with
        member this.MongoDb with get() = this.RequestServices.GetRequiredService<IMongoDatabase>()

[<RequireQualifiedAccess>]
module Dashboard =
    open FbApp.Core
    open FbApp.Domain
    open FbApp.Web.Configuration
    open FbApp.Web
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Options

    type CompetitionItem =
        {
            Label: string
            Value: int64
        }

    let getCompetitionSources year: HttpHandler =
        (fun next context ->
            task {
                if year < 2016 then
                    return! Successful.OK [||] next context
                else
                    let authOptions = context.RequestServices.GetService<IOptions<AuthOptions>>().Value
                    let! competitions = FootballData.getCompetitions authOptions.FootballDataToken [FootballData.Season year]
                    match competitions with
                    | Ok(competitions) ->
                        let competitions = competitions |> Array.map (fun x -> { Label = $"%s{x.Caption} (%s{x.League})"; Value = x.Id })
                        return! Successful.OK competitions next context
                    | Error(_,_,err) ->
                        return! RequestErrors.BAD_REQUEST err.Error next context
            })

    let addCompetition: HttpHandler =
        (fun next context ->
            task {
                let! input = context.BindJsonAsync<Competitions.CreateInput>()
                let command = Competitions.Create input
                let id = Competitions.createId input.ExternalId
                let! result = CommandHandlers.competitionsHandler (id, Aggregate.New) command
                match result with
                | Ok _ -> return! Successful.ACCEPTED id next context
                | Error _ -> return! RequestErrors.CONFLICT "Competition already exists" next context
            })

    let getCompetitions: HttpHandler =
        (fun next context ->
            task {
                let! competitions = context.MongoDb |> Repositories.Competitions.getAll
                return! Successful.OK competitions next context
            })

    let routes = router {
        get "/competitions" getCompetitions
        getf "/competition_sources/%i" getCompetitionSources
        post "/competition/add" addCompetition
    }

[<RequireQualifiedAccess>]
module Fixtures =
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
            Id: Uuid
            Date: DateTimeOffset
            PreviousFixtureId: Nullable<Uuid>
            NextFixtureId: Nullable<Uuid>
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

    let getFixture (id: Uuid) : HttpHandler =
        (fun next ctx -> task {
            let! fixture = Fixtures.get ctx.MongoDb id
            let dto = FixtureDto.FromProjection(fixture)
            return! Successful.OK dto next ctx
        })

    let getFixtureStatus (id: Uuid) : HttpHandler =
        (fun next ctx -> task {
            let! dto = Fixtures.getFixtureStatus ctx.MongoDb id
            return! Successful.OK dto next ctx
        })

    let getTimelyFixture : HttpHandler =
        (fun next ctx -> task {
            let! fixture = Fixtures.getTimelyFixture ctx.MongoDb
            let dto = FixtureDto.FromProjection(fixture)
            return! Successful.OK dto next ctx
        })

    let routes = router {
        getf "/%O" getFixture
        getf "/%O/status" getFixtureStatus
        get "/timely" getTimelyFixture
    }

[<RequireQualifiedAccess>]
module Leagues =
    open FbApp.Core
    open FbApp.Domain

    let private getLeague (code: string) : HttpHandler =
        (fun next ctx -> task {
            code |> ignore
            return! Successful.OK null next ctx
        })

    let private getDefaultLeague : HttpHandler =
        (fun next ctx -> task {
            return! Successful.OK null next ctx
        })

    let private addLeague : HttpHandler =
        (fun next ctx -> task {
            let! input = ctx.BindJsonAsync<Leagues.CreateLeagueInput>()
            let id = Leagues.createId (input.CompetitionId, input.Code)
            let! result = CommandHandlers.leaguesHandler (id, Aggregate.New) (Leagues.Create input)
            match result with
            | Ok _ -> return! Successful.ACCEPTED id next ctx
            | Error _ -> return! RequestErrors.CONFLICT "League already exists" next ctx
        })

    let private addPrediction (leagueId: string, predictionId: string) : HttpHandler =
        (fun next ctx -> task {
            let leagueId = Uuid leagueId
            let predictionId = Uuid predictionId
            let! result = CommandHandlers.leaguesHandler (leagueId, Aggregate.Any) (Leagues.AddPrediction predictionId)
            match result with
            | Ok _ -> return! Successful.ACCEPTED predictionId next ctx
            | Error(e) -> return! RequestErrors.BAD_REQUEST e next ctx
        })

    let private getLeagues : HttpHandler =
        (fun next ctx -> task {
            let! leagues = Repositories.Leagues.getAll ctx.MongoDb
            return! Successful.OK leagues next ctx
        })

    let routes = router {
        get "/" getDefaultLeague
        getf "/league/%s" getLeague

        forward "/admin" (router {
            pipe_through Auth.authPipe
            pipe_through Auth.validateXsrfToken
            pipe_through Auth.adminPipe

            get "/" getLeagues

            post "/" addLeague
            postf "/%s/%s" addPrediction
        })
    }

[<RequireQualifiedAccess>]
module Predict =
    open FbApp.Core
    open FbApp.Domain
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
            CompetitionId: Uuid
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
            CompetitionId: Uuid
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
            let! activeCompetition = Competitions.getActive context.MongoDb
            match activeCompetition with
            | Some competition ->
                let fixtures =
                    {
                        CompetitionId =
                            competition.Id
                        Teams =
                            (competition |> mapTeams)
                        Fixtures =
                            competition.Fixtures
                            |> Array.map (fun x -> { Id = x.ExternalId; HomeTeamId = x.HomeTeamId; AwayTeamId = x.AwayTeamId })
                        Groups =
                            competition.Groups
                    }
                return! Successful.OK fixtures next context
            | None ->
                return! RequestErrors.NOT_FOUND "No active competition" next context
        })

    let private savePredictions: HttpHandler =
        (fun next context -> task {
            let! dto = context.BindJsonAsync<Predictions.PredictionRegistrationInput>()
            let user = Auth.createUser context.User context
            let! competition = Competitions.get context.MongoDb dto.CompetitionId
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

    let private getCurrentPrediction: HttpHandler =
        (fun next context -> task {
            let! activeCompetition = Competitions.getActive context.MongoDb
            match activeCompetition with
            | Some competition ->
                let user = Auth.createUser context.User context
                let! prediction = Predictions.get context.MongoDb (competition.Id, user.Email)
                match prediction with
                | Some(prediction) ->
                    let mapFixture (fixture: ReadModels.PredictionFixtureResult) : PredictionFixtureDto =
                        let x = competition.Fixtures |> Array.find (fun n -> n.ExternalId = fixture.FixtureId)
                        {
                            Fixture = fixture.FixtureId
                            HomeTeam = x.HomeTeamId
                            AwayTeam = x.AwayTeamId
                            Result = fixture.PredictedResult
                        }
                    let dto: PredictionDto =
                        {
                            CompetitionId = competition.Id
                            Teams = (competition |> mapTeams)
                            Fixtures = prediction.Fixtures |> Array.map mapFixture
                            RoundOf16 = prediction.QualifiersRoundOf16 |> Array.map (fun x -> x.Id)
                            RoundOf8 = prediction.QualifiersRoundOf8 |> Array.map (fun x -> x.Id)
                            RoundOf4 = prediction.QualifiersRoundOf4 |> Array.map (fun x -> x.Id)
                            RoundOf2 = prediction.QualifiersRoundOf2 |> Array.map (fun x -> x.Id)
                            Winner = prediction.Winner.Id
                        }
                    return! Successful.OK dto next context
                | None ->
                    return! RequestErrors.NOT_FOUND "Prediction does not exist" next context
            | None ->
                return! RequestErrors.NOT_FOUND "No active competition" next context
        })

    let getCompetitionStatus db = task {
        let! competition = Competitions.getActive db
        return competition |> Option.map (fun x -> if x.Date < DateTimeOffset.Now then "competition-running" else "accept-predictions")
                           |> Option.defaultValue "accept-predicitions"
    }

    let routes = router {
        post "/" (Auth.authPipe >=> Auth.validateXsrfToken >=> savePredictions)
        get "/fixtures" getFixtures
        get "/current" (Auth.authPipe >=> Auth.validateXsrfToken >=> getCurrentPrediction)
    }

[<RequireQualifiedAccess>]
module Predictions =
    let private fixName (name: string) =
        name.Split([|' '|], 2).[0]

    let getScoreTable : HttpHandler =
        (fun next ctx -> task {
            let! competition = Competitions.getActive ctx.MongoDb
            match competition with
            | Some competition ->
                let! scoreTable = Predictions.getScoreTable ctx.MongoDb competition.Id
                let scoreTable = scoreTable |> Seq.map (fun x -> { x with Name = fixName x.Name }) |> Seq.toArray
                return! Successful.OK scoreTable next ctx
            | None ->
                return! RequestErrors.NOT_FOUND "No active competition" next ctx
        })

    let findPredictions term : HttpHandler =
        (fun next ctx -> task {
            let! competition = Competitions.getActive ctx.MongoDb
            match competition with
            | Some competition ->
                let! predictions = Predictions.find ctx.MongoDb competition.Id term
                return! Successful.OK predictions next ctx
            | None ->
                return! RequestErrors.NOT_FOUND "No active competition" next ctx
        })

    let routes = router {
        get "/score" getScoreTable

        forward "/admin" (router {
            pipe_through Auth.authPipe
            pipe_through Auth.validateXsrfToken
            pipe_through Auth.adminPipe

            getf "/search/%s" findPredictions
        })
    }


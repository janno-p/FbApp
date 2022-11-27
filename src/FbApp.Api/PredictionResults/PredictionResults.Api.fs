module FbApp.PredictionResults.Api

open FbApp.Common.SimpleTypes
open FbApp.PredictionResults.Dto
open FbApp.PredictionResults.Queries
open Giraffe
open Microsoft.Extensions.Logging


let getLeaderboard : HttpHandler =
    fun next ctx -> task {
        let competitionId = CompetitionId.create 2000L
        ctx.GetLogger(nameof getLeaderboard).LogInformation("Loading leaderboard of competition {CompetitionId}", CompetitionId.value competitionId)
        let! leaderboard = getLeaderboard (ctx.GetService<_>()) competitionId
        let dto = leaderboard |> Seq.map PredictionResultDto.fromPredictionResult |> Seq.toArray
        return! Successful.OK dto next ctx
    }

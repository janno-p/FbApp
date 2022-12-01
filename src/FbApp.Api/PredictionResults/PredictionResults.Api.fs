module FbApp.PredictionResults.Api

open FbApp.Common.SimpleTypes
open FbApp.PredictionResults.Dto
open FbApp.PredictionResults.ReadModel
open Giraffe
open Microsoft.Extensions.Logging


let getLeaderboard : HttpHandler =
    fun next ctx -> task {
        let competitionId = CompetitionId.create 2000L
        ctx.GetLogger(nameof getPredictionResults).LogInformation("Loading leaderboard of competition {CompetitionId}", CompetitionId.value competitionId)
        let dto =
            getPredictionResults ()
            |> List.map PredictionResultDto.fromPredictionResult
            |> List.groupBy (fun x -> (x.Total, x.Ratio))
            |> List.sortByDescending fst
            |> List.fold
                   (fun (arr: ResizeArray<_>) (_, items) ->
                        let pos = arr.Count + 1
                        items |> List.iter (fun item -> arr.Add({ item with Rank = pos }))
                        arr
                   )
                   (ResizeArray<PredictionResultDto>())
            |> Seq.toArray
        return! Successful.OK dto next ctx
    }

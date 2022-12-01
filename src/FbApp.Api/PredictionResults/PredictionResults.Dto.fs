module FbApp.PredictionResults.Dto

open FbApp.Common
open FbApp.PredictionResults.ReadModel


type PredictionResultDto = {
    Name: string
    Points: int array
    Total: int
    Rank: int
    Ratio: double
}


module PredictionResultDto =

    let fromPredictionResult (predictionResult: PredictionResult) =
        let getScore (det: DetailedScore) =
            [
                1 * det.Matches.Count
                2 * det.Qualifiers.Count
                3 * det.QuarterFinals.Count
                4 * det.SemiFinals.Count
                5 * det.Finals.Count
                6 * (det.Winner |> Option.map (fun _ -> 1) |> Option.defaultValue 0)
                5 * (det.TopScorer |> Option.map (fun _ -> 1) |> Option.defaultValue 0)
            ]
            |> List.sum
        {
            Name = Helpers.excludeLastName predictionResult.Name
            Points =
                [|
                    1 * predictionResult.GainedPoints.Matches.Count
                    2 * predictionResult.GainedPoints.Qualifiers.Count
                    3 * predictionResult.GainedPoints.QuarterFinals.Count
                    4 * predictionResult.GainedPoints.SemiFinals.Count
                    5 * predictionResult.GainedPoints.Finals.Count
                    6 * (predictionResult.GainedPoints.Winner |> Option.map (fun _ -> 1) |> Option.defaultValue 0)
                    5 * (predictionResult.GainedPoints.TopScorer |> Option.map (fun _ -> 1) |> Option.defaultValue 0)
                |]
            Total = getScore predictionResult.GainedPoints
            Ratio = double (MaxTotalPoints - getScore predictionResult.LostPoints) / double MaxTotalPoints * 100.0
            Rank = 0
        }

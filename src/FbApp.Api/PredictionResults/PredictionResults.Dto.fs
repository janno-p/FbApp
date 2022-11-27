module FbApp.PredictionResults.Dto

open System
open FbApp.Common
open FbApp.PredictionResults.Queries


type PredictionResultDto = {
    Id: Guid
    Name: string
    Points: double array
    Total: double
    Ratio: double
    Rank: int32
}


module PredictionResultDto =

    let fromPredictionResult (predictionResult: PredictionResult) =
        {
            Id = predictionResult.Id
            Name = Helpers.excludeLastName predictionResult.Name
            Points = predictionResult.Points
            Total = predictionResult.Total
            Ratio = predictionResult.Ratio
            Rank = predictionResult.Rank
        }

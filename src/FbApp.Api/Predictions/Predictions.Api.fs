module FbApp.Predictions.Api

open FbApp.Api
open FbApp.Api.Auth
open FbApp.Predictions.Queries
open Giraffe


let getUserPrediction : AuthHttpHandler =
    fun user next ctx -> task {
        let! predictionId = getUserPrediction FootballData.ActiveCompetition (ctx.GetService<_>()) user.Email
        let dto = predictionId |> Option.toNullable
        return! Successful.OK dto next ctx
    }

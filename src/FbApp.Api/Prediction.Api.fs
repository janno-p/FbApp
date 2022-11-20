module FbApp.Prediction.Api

open FbApp.Api.Auth
open FbApp.Prediction.Queries
open Giraffe


let getUserPrediction : AuthHttpHandler =
    fun user next ctx -> task {
        let! predictionId = getUserPrediction (ctx.GetService<_>()) user.Email
        let dto = predictionId |> Option.toNullable
        return! Successful.OK dto next ctx
    }

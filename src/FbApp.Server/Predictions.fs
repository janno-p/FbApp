module FbApp.Server.Predictions

open FbApp.Server.Repositories
open FSharp.Control.Tasks
open Giraffe
open Saturn

let private fixName (name: string) =
    name.Split([|' '|], 2).[0]

let getScoreTable : HttpHandler =
    (fun next ctx -> task {
        let! competition = Competitions.getActive ()
        let! scoreTable = Predictions.getScoreTable competition.Id
        let scoreTable = scoreTable |> Seq.map (fun x -> { x with Name = fixName x.Name }) |> Seq.toArray
        return! Successful.OK scoreTable next ctx
    })

let findPredictions term : HttpHandler =
    (fun next ctx -> task {
        let! competition = Competitions.getActive ()
        let! predictions = Predictions.find competition.Id term
        return! Successful.OK predictions next ctx
    })

let scope = router {
    get "/score" getScoreTable

    forward "/admin" (router {
        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        getf "/search/%s" findPredictions
    })
}

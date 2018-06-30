module FbApp.Server.Predictions

open FbApp.Server.Repositories
open Giraffe
open Saturn

let findPredictions term : HttpHandler =
    (fun next ctx -> task {
        let! competition = Competitions.getActive ()
        let! predictions = Predictions.find competition.Id term
        return! Successful.OK predictions next ctx
    })

let scope = scope {
    forward "/admin" (scope {
        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        getf "/search/%s" findPredictions
    })
}

module FbApp.Server.Leagues

open Giraffe
open Saturn
open System

let private getLeague (name: string) : HttpHandler =
    (fun next ctx -> task {
        return! Successful.OK null next ctx
    })

let private getDefaultLeague : HttpHandler =
    (fun next ctx -> task {
        return! Successful.OK null next ctx
    })

let private addLeague : HttpHandler =
    (fun next ctx -> task {
        return! Successful.OK null next ctx
    })

let private addPrediction (leagueId: Guid, predictionId: Guid) : HttpHandler =
    (fun next ctx -> task {
        return! Successful.OK null next ctx
    })

let private getLeagues : HttpHandler =
    (fun next ctx -> task {
        return! Successful.OK [] next ctx
    })

let scope = scope {
    get "/" getDefaultLeague
    getf "/%s" getLeague

    forward "/admin" (scope {
        pipe_through Auth.authPipe
        pipe_through Auth.validateXsrfToken
        pipe_through Auth.adminPipe

        get "/" getLeagues

        post "/" addLeague
        postf "/%O/%O" addPrediction
    })
}

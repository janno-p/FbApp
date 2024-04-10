module internal FbApp.Modules.UserAccess.Endpoints

open Giraffe.Core
open Giraffe.EndpointRouting
open Giraffe.HttpStatusCodeHandlers


[<RequireQualifiedAccess>]
module internal Route =
    let [<Literal>] Authorize = "/connect/authorize"
    let [<Literal>] Google = "/connect/google"
    let [<Literal>] GoogleComplete = "/connect/google/complete"
    let [<Literal>] Logout = "/connect/logout"
    let [<Literal>] Token = "/connect/token"
    let [<Literal>] Userinfo = "/connect/userinfo"


let notImplemented name = ServerErrors.notImplemented (text $"Endpoint '%s{name}' is not implemented")


let authorize: HttpHandler = notImplemented "authorize"
let googleLogin: HttpHandler = notImplemented "google login"
let googleResponse: HttpHandler = notImplemented "google response"
let logout: HttpHandler = notImplemented "logout"
let userInfo: HttpHandler = notImplemented "user info"
let exchangeToken: HttpHandler = notImplemented "exchange token"


let userAccess = [
    GET [
        route Route.Authorize authorize
        route Route.Google googleLogin
        route Route.GoogleComplete googleResponse
        route Route.Logout logout
        route Route.Userinfo userInfo
    ]
    POST [
        route Route.Token exchangeToken
    ]
]

[<RequireQualifiedAccessAttribute>]
module FbApp.Shared.Routes

module Site =
    let [<Literal>] Home = "/"
    let [<Literal>] Changelog = "/changelog"
    let [<Literal>] NotFound = "/not-found"
    let [<Literal>] Dashboard = "/dashboard"

module UserAccess =
    let [<Literal>] GoogleLogin = "/user-access/google"
    let [<Literal>] GoogleCallback = "/user-access/google/callback"
    let [<Literal>] GoogleComplete = "/user-access/google/complete"
    let [<Literal>] Logout = "/user-access/logout"

module Dashboard =
    module Competitions =
        let [<Literal>] list = "/dashboard/competitions"
        let [<Literal>] add = "/dashboard/competitions/add"

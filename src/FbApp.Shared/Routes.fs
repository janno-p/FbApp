[<RequireQualifiedAccessAttribute>]
module FbApp.Shared.Routes

module Site =
    let [<Literal>] Home = "/"
    let [<Literal>] Changelog = "/changelog"
    let [<Literal>] NotFound = "/not-found"

    let [<Literal>] GoogleLogin = "/user-access/google"
    let [<Literal>] Logout = "/user-access/logout"

    let [<Literal>] Dashboard = "/dashboard"

module Dashboard =
    module Competitions =
        let [<Literal>] list = "/dashboard/competitions"
        let [<Literal>] add = "/dashboard/competitions/add"

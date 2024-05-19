[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.Routes


let [<Literal>] Home = "/"
let [<Literal>] Changelog = "/changelog"
let [<Literal>] NotFound = "/not-found"

let [<Literal>] GoogleLogin = "/user-access/google"
let [<Literal>] Logout = "/user-access/logout"


[<RequireQualifiedAccess>]
module Dashboard =
    let [<Literal>] root = "/dashboard"
    let [<Literal>] index = "/dashboard/index"
    let [<Literal>] competitions = "/dashboard/competitions"

    let page (name: string) = $"/dashboard?page=%s{name}"

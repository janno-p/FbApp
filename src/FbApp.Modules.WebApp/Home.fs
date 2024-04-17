[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.Home

open Oxpecker.ViewEngine


let view = {|
    Title = "Home"
    Content = h1() { "Home!" }
|}

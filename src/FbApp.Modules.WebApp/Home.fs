[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.Home

open Oxpecker.ViewEngine

open Types


let view: View = {
    Title = "Home"
    Content = h1() { "Home!" }
}

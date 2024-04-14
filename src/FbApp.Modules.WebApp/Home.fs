module internal FbApp.Modules.WebApp.Home

open Giraffe.ViewEngine


let view = {|
    Title = "Home"
    Content = h1 [] [encodedText "Home!"]
|}

module FbApp.Server.Common

open Giraffe

[<CLIMutable>]
type AuthOptions =
    {
        AdminEmails: string[]
        FootballDataToken: string
    }

[<CLIMutable>]
type GoogleOptions =
    {
        ClientId: string
    }

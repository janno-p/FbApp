module FbApp.Server.Common

open Giraffe

let notAuthorized: HttpHandler =
    RequestErrors.FORBIDDEN "Not Authorized"

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

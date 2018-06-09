module FbApp.Server.Common

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

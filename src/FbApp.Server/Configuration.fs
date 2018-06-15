namespace FbApp.Server.Configuration

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

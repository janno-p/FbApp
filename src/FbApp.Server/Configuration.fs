namespace FbApp.Server.Configuration

[<CLIMutable>]
type AuthOptions = {
    AdminEmails: string[]
    FootballDataToken: string
    }

[<CLIMutable>]
type GoogleOptions = {
    ClientId: string
    }

[<CLIMutable>]
type SubscriptionsSettings = {
    StreamName: string
    ProjectionsGroup: string
    ProcessManagerGroup: string
    }

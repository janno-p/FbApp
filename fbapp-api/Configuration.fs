namespace FbApp.Api.Configuration

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
    GroupName: string
    }

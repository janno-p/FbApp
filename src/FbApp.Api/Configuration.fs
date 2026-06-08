namespace FbApp.Api.Configuration

[<CLIMutable>]
type AuthOptions = {
    FootballDataToken: string
    }

[<CLIMutable>]
type SubscriptionsSettings = {
    StreamName: string
    GroupName: string
    Reset: bool
    }

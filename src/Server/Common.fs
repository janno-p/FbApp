module FbApp.Server.Common

open EventStore.ClientAPI
open EventStore.ClientAPI.SystemData
open Giraffe
open System

let notAuthorized: HttpHandler =
    RequestErrors.FORBIDDEN "Not Authorized"

[<CLIMutable>]
type AuthOptions =
    {
        AdminEmails: string[]
    }

[<CLIMutable>]
type GoogleOptions =
    {
        ClientId: string
    }

let eventStore =
    let settings =
        ConnectionSettings
            .Create()
            .UseConsoleLogger()
            .EnableVerboseLogging()
            .SetDefaultUserCredentials(UserCredentials("admin", "changeit"))
            .Build()

    EventStoreConnection.Create(settings, Uri("tcp://localhost:1113"))

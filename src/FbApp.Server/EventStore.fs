[<RequireQualifiedAccess>]
module FbApp.Server.EventStore

(*
open FbApp.Core.EventStore
open EventStore.ClientAPI
open EventStore.ClientAPI.Common.Log
open EventStore.ClientAPI.Projections
open FSharp.Control.Tasks.ContextInsensitive
open System
open System.Net
*)

let [<Literal>] DomainEventsStreamName = "domain-events"
let [<Literal>] ProjectionsSubscriptionGroup = "projections"
let [<Literal>] ProcessManagerSubscriptionGroup = "process-manager"

(*
let initProjectionsAndSubscriptions (connection: IEventStoreConnection, options: EventStoreOptions) = task {
    let logger = ConsoleLogger()

    let projectionsManager = ProjectionsManager(logger, IPEndPoint(IPAddress.Loopback, 2113), TimeSpan.FromSeconds(5.0))

    let query = (sprintf """fromAll()
.when({
    $any: function (state, ev) {
        if (ev.metadata !== null && ev.metadata.applicationName === "%s") {
            linkTo("%s", ev)
        }
    }
})""" ApplicationName DomainEventsStreamName)

    let withExceptionLogging f = task {
        try
            do! f()
        with e -> logger.Info(e, e.Message)
    }

    do! withExceptionLogging (fun () -> task {
        do! projectionsManager.CreateContinuousAsync(DomainEventsStreamName, query, options.UserCredentials)
    })

    let settings = PersistentSubscriptionSettings.Create().ResolveLinkTos().StartFromBeginning().Build()

    do! withExceptionLogging (fun () -> task {
        do! connection.CreatePersistentSubscriptionAsync(DomainEventsStreamName, ProjectionsSubscriptionGroup, settings, null)
    })

    do! withExceptionLogging (fun () -> task {
        do! connection.CreatePersistentSubscriptionAsync(DomainEventsStreamName, ProcessManagerSubscriptionGroup, settings, null)
    })
}
*)

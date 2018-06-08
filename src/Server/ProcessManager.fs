module FbApp.Server.ProcessManager

open EventStore.ClientAPI
open EventStore.ClientAPI.Exceptions
open Giraffe
open Microsoft.Extensions.Logging

let eventAppeared (log: ILogger) (subscription: EventStorePersistentSubscriptionBase) (e: ResolvedEvent) : System.Threading.Tasks.Task = upcast task {
    try
        match EventStore.getMetadata e with
        | Some(md) when md.AggregateName = "Competition" ->
            match Serialization.deserializeOf<Competition.Event> (e.Event.EventType, e.Event.Data) with
            | Competition.Created args ->
                try
                    let! teams = FootballData.loadCompetitionTeams args.ExternalSource
                    let! fixtures = FootballData.loadCompetitionFixtures args.ExternalSource
                    let! groups = FootballData.loadCompetitionGroups args.ExternalSource
                    let command = Competition.Command.AssignTeamsAndFixtures (teams |> List.ofArray, fixtures |> List.ofArray, groups)
                    let! _ = Aggregate.Handlers.competitionHandler (md.AggregateId, Some(md.AggregateSequenceNumber)) command
                    ()
                with :? WrongExpectedVersionException as ex ->
                    log.LogInformation(ex, "Cannot process current event: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
            | _ -> ()
        | _ -> ()
        subscription.Acknowledge(e)
    with ex ->
        log.LogError(ex, "Process manager error with event {0} {1}.", e.OriginalStreamId, e.OriginalEventNumber)
        subscription.Fail(e, PersistentSubscriptionNakEventAction.Retry, "unexpected exception occured")
}

type private X = class end

let connectSubscription (connection: IEventStoreConnection) (loggerFactory: ILoggerFactory) =
    let log = loggerFactory.CreateLogger(typeof<X>.DeclaringType)
    connection.ConnectToPersistentSubscription("domain-events", "process-manager", (eventAppeared log), autoAck = false) |> ignore

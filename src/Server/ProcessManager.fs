module FbApp.Server.ProcessManager

open EventStore.ClientAPI
open Giraffe

let eventAppeared (subscription: EventStorePersistentSubscriptionBase) (e: ResolvedEvent) : System.Threading.Tasks.Task = upcast task {
    try
        let md: EventStore.Metadata = Serialization.deserializeType e.Event.Metadata
        match Serialization.deserializeOf<Competition.Event> (e.Event.EventType, e.Event.Data) with
        | Competition.Created args ->
            let! teams = FootballData.loadCompetitionTeams args.ExternalSource
            let teamsCommand = Competition.Command.AssignTeams (teams |> List.ofArray)
            let! version = Aggregate.Handlers.competitionHandler (md.AggregateId, md.AggregateSequenceNumber) teamsCommand
            match version with
            | Ok(v) ->
                let! fixtures = FootballData.loadCompetitionFixtures args.ExternalSource
                let fixturesCommand = Competition.Command.AssignFixtures (fixtures |> List.ofArray)
                let! _ = Aggregate.Handlers.competitionHandler (md.AggregateId, v) fixturesCommand
                ()
            | _ -> ()
        | _ -> ()
    with e ->
        printfn "Error fixtures: %A" e
        raise e
}

let connectSubscription (connection: IEventStoreConnection) =
    connection.ConnectToPersistentSubscription("$ce-Competition", "process-manager", eventAppeared) |> ignore

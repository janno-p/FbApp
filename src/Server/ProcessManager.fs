module FbApp.Server.ProcessManager

open EventStore.ClientAPI
open EventStore.ClientAPI.Exceptions
open Giraffe
open Microsoft.Extensions.Logging
open FbApp.Server
open FbApp.Server.Common

module Result =
    let unwrap f = function
        | Ok(x) -> x
        | Error(x) -> failwithf "%A" (f x)

let eventAppeared (log: ILogger, authOptions: AuthOptions) (subscription: EventStorePersistentSubscriptionBase) (e: ResolvedEvent) : System.Threading.Tasks.Task = upcast task {
    try
        match EventStore.getMetadata e with
        | Some(md) when md.AggregateName = "Competition" ->
            match Serialization.deserializeOf<Competition.Event> (e.Event.EventType, e.Event.Data) with
            | Competition.Created args ->
                try
                    let! teams = FootballData.getCompetitionTeams authOptions.FootballDataToken args.ExternalSource
                    let teams = teams |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)
                    let! fixtures = FootballData.getCompetitionFixtures authOptions.FootballDataToken args.ExternalSource []
                    let fixtures = fixtures |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)
                    let! groups = FootballData.getCompetitionLeagueTable authOptions.FootballDataToken args.ExternalSource []
                    let groups =
                        match (groups |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)).Standings with
                        | FootballData.Groups groups -> groups
                        | _ -> failwith "Leagues are not implemented"
                    let command =
                        Competition.Command.AssignTeamsAndFixtures
                            (teams.Teams |> Seq.map (fun x -> { Name = x.Name; Code = x.Code; FlagUrl = x.CrestUrl; ExternalId = x.Id } : Competition.TeamAssignment) |> Seq.toList,
                             fixtures.Fixtures |> Seq.map (fun x -> { HomeTeamId = x.HomeTeamId; AwayTeamId = x.AwayTeamId; Date = x.Date; ExternalId = x.Id } : Competition.FixtureAssignment) |> Seq.toList,
                             groups |> Seq.map (fun kvp -> kvp.Key, (kvp.Value |> Array.map (fun x -> x.TeamId))) |> Seq.toList)
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

let connectSubscription (connection: IEventStoreConnection) (loggerFactory: ILoggerFactory) (authOptions: AuthOptions) =
    let log = loggerFactory.CreateLogger(typeof<X>.DeclaringType)
    connection.ConnectToPersistentSubscription(EventStore.DomainEventsStreamName, EventStore.ProcessManagerSubscriptionGroup, (eventAppeared (log, authOptions)), autoAck = false) |> ignore

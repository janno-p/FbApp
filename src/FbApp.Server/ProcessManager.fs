module FbApp.Server.ProcessManager

open EventStore.Client
open FbApp.Core
open FbApp.Core.EventStore
open FbApp.Core.Serialization
open FbApp.Domain
open FbApp.Server.Repositories
open FSharp.Control.Tasks
open Giraffe
open Microsoft.Extensions.Logging
open FbApp.Server
open FbApp.Server.Configuration

module Result =
    let unwrap f = function
        | Ok(x) -> x
        | Error(x) -> failwithf "%A" (f x)

let processCompetitions (log: ILogger) (authOptions: AuthOptions) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Competitions.Event> (e.Event.EventType, e.Event.Data) with
    | Competitions.Created args ->
        try
            let! teams = FootballData.getCompetitionTeams authOptions.FootballDataToken args.ExternalId
            let teams = teams |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)
            let! fixtures = FootballData.getCompetitionFixtures authOptions.FootballDataToken args.ExternalId []
            let fixtures = fixtures |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)
            let! groups = FootballData.getCompetitionLeagueTable authOptions.FootballDataToken args.ExternalId []
            let groups =
                match (groups |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)).Standings with
                | FootballData.Groups groups -> groups
                | _ -> failwith "Leagues are not implemented"
            let command =
                Competitions.Command.AssignTeamsAndFixtures
                    (teams.Teams |> Seq.map (fun x -> { Name = x.Name; Code = x.Code; FlagUrl = x.CrestUrl; ExternalId = x.Id } : Competitions.TeamAssignment) |> Seq.toList,
                        fixtures.Fixtures |> Seq.filter (fun f -> f.Matchday < 4) |> Seq.map (fun x -> { HomeTeamId = x.HomeTeamId; AwayTeamId = x.AwayTeamId; Date = x.Date; ExternalId = x.Id } : Competitions.FixtureAssignment) |> Seq.toList,
                        groups |> Seq.map (fun kvp -> kvp.Key, (kvp.Value |> Array.map (fun x -> x.TeamId))) |> Seq.toList)
            let id = Competitions.createId args.ExternalId
            let! _ = CommandHandlers.competitionsHandler (id, Aggregate.Version md.AggregateSequenceNumber) command
            ()
        with :? WrongExpectedVersionException as ex ->
            log.LogInformation(ex, "Cannot process current event: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Competitions.FixturesAssigned fixtures ->
        for fixture in fixtures do
            try
                let fixtureId = Fixtures.createId (md.AggregateId, fixture.ExternalId)
                let input : Fixtures.AddFixtureInput =
                    {
                        CompetitionId = md.AggregateId
                        ExternalId = fixture.ExternalId
                        HomeTeamId = fixture.HomeTeamId
                        AwayTeamId = fixture.AwayTeamId
                        Date = fixture.Date
                        Status = "SCHEDULED"
                        Stage = "GROUP_STAGE"
                    }
                let! _ = CommandHandlers.fixturesHandler (fixtureId, Aggregate.New) (Fixtures.AddFixture input)
                ()
            with :? WrongExpectedVersionException as ex ->
                log.LogInformation(ex, "Cannot process current event: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | _ -> ()
}

let processPredictions (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Predictions.Event> (e.Event.EventType, e.Event.Data) with
    | Predictions.Registered args ->
        let! competition = Competitions.get args.CompetitionId
        if md.Timestamp > competition.Value.Date then
            let id = Predictions.createId (args.CompetitionId, Predictions.Email args.Email)
            let! _ = CommandHandlers.predictionsHandler (id, Aggregate.Any) Predictions.Decline
            ()
    | _ -> ()
}

let eventAppeared (log: ILogger, authOptions: AuthOptions) (e: ResolvedEvent) : System.Threading.Tasks.Task = unitTask {
    try
        match getMetadata e with
        | Some(md) when md.AggregateName = Competitions.AggregateName ->
            do! processCompetitions log authOptions md e
        | Some(md) when md.AggregateName = Predictions.AggregateName ->
            do! processPredictions md e
        | _ -> ()
    with ex ->
        log.LogError(ex, "Process manager error with event {0} {1}.", e.OriginalStreamId, e.OriginalEventNumber)
        raise ex
}

type private X = class end

let connectSubscription (client: EventStoreClient) (loggerFactory: ILoggerFactory) (authOptions: AuthOptions) = unitTask {
    let log = loggerFactory.CreateLogger(typeof<X>.DeclaringType)
    let! _ = client.SubscribeToStreamAsync(EventStore.DomainEventsStreamName, fun _ e _ -> eventAppeared (log, authOptions) e)
    ()
}

module FbApp.Api.ProcessManager


open EventStore.Client
open FbApp.Api.Configuration
open FbApp.Api.Domain
open FbApp.Api.EventStore
open FbApp.Api.Repositories
open FbApp.Api.Serialization
open FSharp.Control.Tasks
open Microsoft.Extensions.Logging
open MongoDB.Driver
open System
open System.Collections.Generic


module Result =
    let unwrap f = function
        | Ok(x) -> x
        | Error(x) -> failwithf "%A" (f x)


let (|Nullable|_|) (x: Nullable<_>) =
    if x.HasValue then Some(x.Value) else None


let upsertCompetition (logger: ILogger) (metadata: Metadata) (e: ResolvedEvent) (model: Competitions.Created) = unitTask {
    try
        let competitionModel: ReadModels.Competition =
            {
                Id =  metadata.AggregateId
                Description = model.Description
                ExternalId = model.ExternalId
                Teams = [||]
                Fixtures = [||]
                Groups = Dictionary<_,_>()
                Version = metadata.AggregateSequenceNumber
                Date = model.Date.ToOffset(TimeSpan.Zero)
            }
        do! Competitions.insert competitionModel
    with :? MongoWriteException as ex ->
        logger.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
}


let processCompetitions (logger: ILogger) (authOptions: AuthOptions) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Competitions.Event> (e.Event.EventType, e.Event.Data) with
    | Competitions.Created args ->
        try
            do! args |> upsertCompetition logger md e
            let! teams = FootballData.getCompetitionTeams authOptions.FootballDataToken args.ExternalId
            let teams = teams |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)
            let! fixtures = FootballData.getCompetitionFixtures authOptions.FootballDataToken args.ExternalId []
            let fixtures = fixtures |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)
            let! groups = FootballData.getCompetitionLeagueTable authOptions.FootballDataToken args.ExternalId
            let groups = groups |> Result.unwrap (fun (_,_,err) -> failwithf "%s" err.Error)
            let command =
                Competitions.Command.AssignTeamsAndFixtures
                    (teams.Teams |> Seq.map (fun x -> { Name = x.Name; Code = x.Code; FlagUrl = x.CrestUrl; ExternalId = x.Id } : Competitions.TeamAssignment) |> Seq.toList,
                        fixtures.Fixtures |> Seq.filter (fun f -> f.Matchday < 4) |> Seq.map (fun x -> { HomeTeamId = x.HomeTeam.Value.Id.Value; AwayTeamId = x.AwayTeam.Value.Id.Value; Date = x.Date; ExternalId = x.Id } : Competitions.FixtureAssignment) |> Seq.toList,
                        groups.Standings |> Seq.filter (fun r -> r.Stage = "GROUP_STAGE") |> Seq.map (fun kvp -> kvp.Group, (kvp.Table |> Array.map (fun x -> x.Team.Id))) |> Seq.toList)
            let id = Competitions.createId args.ExternalId
            let! _ = CommandHandlers.competitionsHandler (id, Aggregate.Version md.AggregateSequenceNumber) command
            ()
        with :? WrongExpectedVersionException as ex ->
            logger.LogInformation(ex, "Cannot process current event: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Competitions.TeamsAssigned teams ->
        let teamProjections =
            teams
            |> List.map
                (fun team ->
                    {
                        Name = team.Name
                        Code = team.Code
                        FlagUrl = team.FlagUrl
                        ExternalId = team.ExternalId
                    } : ReadModels.Team)
            |> List.toArray
        do! Competitions.updateTeams (md.AggregateId, md.AggregateSequenceNumber) teamProjections
    | Competitions.FixturesAssigned fixtures ->
        let fixtureProjections =
            fixtures |> List.map (fun t ->
                {
                    HomeTeamId = t.HomeTeamId
                    AwayTeamId = t.AwayTeamId
                    Date = t.Date.ToOffset(TimeSpan.Zero)
                    ExternalId = t.ExternalId
                } : ReadModels.CompetitionFixture
            ) |> List.toArray
        do! Competitions.updateFixtures (md.AggregateId, md.AggregateSequenceNumber) fixtureProjections
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
                logger.LogInformation(ex, "Cannot process current event: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Competitions.GroupsAssigned groups ->
        do! (dict groups) |> Competitions.updateGroups (md.AggregateId, md.AggregateSequenceNumber)
}


let private mapActualResult (status, fullTime, extraTime: int array, penalties: int array) =
    match status, fullTime with
    | "FINISHED", [| homeGoals; awayGoals |] ->
        let et = if extraTime |> isNull then [| 0; 0 |] else extraTime
        let ps = if penalties |> isNull then [| 0; 0 |] else penalties
        let homeGoals = homeGoals + et.[0] + ps.[0]
        let awayGoals = awayGoals + et.[1] + ps.[1]
        if homeGoals > awayGoals then "HomeWin"
        elif homeGoals < awayGoals then "AwayWin"
        else "Tie"
    | _ -> null


let private mapFixtureResult competitionId (x: Predictions.FixtureResultRegistration) = task {
    let mapResult = function
        | Predictions.HomeWin -> "HomeWin"
        | Predictions.Tie -> "Tie"
        | Predictions.AwayWin -> "AwayWin"

    let fixtureId = Fixtures.createId (competitionId, x.Id)
    let! result = Fixtures.getFixtureStatus fixtureId

    let result = mapActualResult (result.Status, result.FullTime, result.ExtraTime, result.Penalties)

    let fixtureResult : ReadModels.PredictionFixtureResult =
        {
            FixtureId = x.Id
            PredictedResult = mapResult x.Result
            ActualResult = result
        }

    return fixtureResult
}


let acceptPrediction (metadata: Metadata) (model: Predictions.PredictionRegistration) = unitTask {
    let fixtureTasks =
        model.Fixtures
        |> Seq.map (mapFixtureResult model.CompetitionId)
        |> Seq.toArray

    let! fixtures = System.Threading.Tasks.Task.WhenAll(fixtureTasks)

    let updates =
        fixtures
        |> Array.map
            (fun fixture -> task {
                let prediction: ReadModels.FixtureResultPrediction =
                    {
                        PredictionId = metadata.AggregateId
                        Name = model.Name
                        Result = fixture.PredictedResult
                    }
                let id = Fixtures.createId (model.CompetitionId, fixture.FixtureId)
                do! Fixtures.addPrediction id prediction
            })

    let! _ = System.Threading.Tasks.Task.WhenAll(updates)

    let prediction: ReadModels.Prediction =
        {
            Id = metadata.AggregateId
            Name = model.Name
            Email = model.Email
            CompetitionId = model.CompetitionId
            Fixtures = fixtures
            QualifiersRoundOf16 = model.Qualifiers.RoundOf16 |> List.map (fun x -> { Id = x; HasQualified = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
            QualifiersRoundOf8 = model.Qualifiers.RoundOf8 |> List.map (fun x -> { Id = x; HasQualified = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
            QualifiersRoundOf4 = model.Qualifiers.RoundOf4 |> List.map (fun x -> { Id = x; HasQualified = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
            QualifiersRoundOf2 = model.Qualifiers.RoundOf2 |> List.map (fun x -> { Id = x; HasQualified = Nullable() } : ReadModels.QualifiersResult) |> List.toArray
            Winner = { Id = model.Winner; HasQualified = Nullable() }
            Leagues = [||]
            Version = metadata.AggregateSequenceNumber
        }
    do! Predictions.insert prediction
}


let processPredictions (logger: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Predictions.Event> (e.Event.EventType, e.Event.Data) with
    | Predictions.Registered args ->
        let! competition = Competitions.get args.CompetitionId
        if md.Timestamp > competition.Value.Date then
            let id = Predictions.createId (args.CompetitionId, Predictions.Email args.Email)
            let! _ = CommandHandlers.predictionsHandler (id, Aggregate.Any) Predictions.Decline
            ()
        else
            try
                do! acceptPrediction md args
            with :? MongoWriteException as ex ->
                logger.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Predictions.Declined ->
        do! Predictions.delete md.AggregateId
}


let updateFixtureOrder competitionId = task {
    let! fixtures = Fixtures.getFixtureOrder competitionId
    match fixtures |> Seq.toList with
    | [] | [_] -> ()
    | [x;y] ->
        if x.PreviousId.HasValue || x.NextId <> Nullable(y.Id) then
            do! Fixtures.setAdjacentFixtures x.Id (None, Some(y.Id))
        if y.PreviousId <> Nullable(x.Id) || y.NextId.HasValue then
            do! Fixtures.setAdjacentFixtures y.Id (Some(x.Id), None)
    | fixtures ->
        let x, y = match fixtures with x::y::_ -> x, y | _ -> failwith "never"
        if x.PreviousId.HasValue || x.NextId <> Nullable(y.Id) then
            do! Fixtures.setAdjacentFixtures x.Id (None, Some(y.Id))
        for w in fixtures |> List.windowed 3 do
            let p, x, n = match w with [p; x; n] -> p, x, n | _ -> failwith "never"
            if x.PreviousId <> Nullable(p.Id) || x.NextId <> Nullable(n.Id) then
                do! Fixtures.setAdjacentFixtures x.Id (Some(p.Id), Some(n.Id))
        let x, y = match fixtures |> List.rev with x::y::_ -> x, y | _ -> failwith "never"
        if x.PreviousId <> Nullable(y.Id) || x.NextId.HasValue then
            do! Fixtures.setAdjacentFixtures x.Id (Some(y.Id), None)
}


let getResultPredictions (input: Fixtures.AddFixtureInput) = task {
    if input.Stage <> "GROUP_STAGE" then return [||] else
    let! predictions = Predictions.ofFixture input.CompetitionId input.ExternalId
    let predictions =
        predictions
        |> Seq.map
            (fun x ->
                {
                    PredictionId = x.Id
                    Name = x.Name
                    Result = x.Fixtures.[0].PredictedResult
                } : ReadModels.FixtureResultPrediction)
        |> Seq.toArray
    return predictions
}


let getQualificationPredictions (input: Fixtures.AddFixtureInput) = task {
    if input.Stage = "GROUP_STAGE" then return [||] else
    let! predictions = Predictions.ofStage (input.CompetitionId, input.Stage)
    let result =
        predictions
        |> Seq.map (fun x ->
            {
                PredictionId = x.Id
                Name = x.Name
                HomeQualifies = x.Qualifiers |> Array.exists (fun z -> z.Id = input.HomeTeamId)
                AwayQualifies = x.Qualifiers |> Array.exists (fun z -> z.Id = input.AwayTeamId)
            } : ReadModels.QualificationPrediction)
        |> Seq.toArray
    return result
}


let updateQualifiedTeams (competition: ReadModels.Competition) = task {
    let! qualifiedTeams = Fixtures.getQualifiedTeams competition.Id
    let unqualifiedTeams = competition.Teams |> Seq.map (fun x -> x.ExternalId) |> Seq.except qualifiedTeams |> Seq.toArray
    do! Predictions.setUnqualifiedTeams (competition.Id, unqualifiedTeams)
}


let updateScore (fixtureId: Guid, expectedVersion: int64, fullTime, extraTime, penalties) = task {
    let! fixture = Fixtures.get fixtureId
    do! Fixtures.updateScore (fixtureId, expectedVersion) (fullTime, extraTime, penalties)
    let ps = match penalties with Some(u) -> [| u.Home; u.Away |] | _ -> [| 0; 0 |]
    let et = match extraTime with Some(u) -> [| u.Home; u.Away |] | _ -> [| 0; 0 |]
    let actualResult = mapActualResult (fixture.Status, [| fullTime.Home; fullTime.Away |], et, ps)
    if fixture.Stage = "GROUP_STAGE" then
        if actualResult |> isNull |> not then
            do! Predictions.updateResult (fixture.CompetitionId, fixture.ExternalId, actualResult)
    else if fixture.Status = "FINISHED" then
        do! Predictions.updateQualifiers (fixture.CompetitionId, fixture.Stage, fixture.HomeTeam.ExternalId, actualResult = "HomeWin")
        do! Predictions.updateQualifiers (fixture.CompetitionId, fixture.Stage, fixture.AwayTeam.ExternalId, actualResult = "AwayWin")
}


let processFixtures (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Fixtures.Event> (e.Event.EventType, e.Event.Data) with
    | Fixtures.Added input ->
        try
            let! competition = Competitions.get input.CompetitionId
            let competition = competition |> Option.get

            let homeTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.HomeTeamId)
            let awayTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.AwayTeamId)

            let! resultPredictions = getResultPredictions input
            let! qualificationPredictions = getQualificationPredictions input

            let stage = if input.Stage |> String.IsNullOrEmpty then "GROUP_STAGE" else input.Stage

            let fixtureModel: ReadModels.Fixture =
                {
                    Id = md.AggregateId
                    CompetitionId = input.CompetitionId
                    Date = input.Date.ToOffset(TimeSpan.Zero)
                    PreviousId = Nullable()
                    NextId = Nullable()
                    HomeTeam = homeTeam
                    AwayTeam = awayTeam
                    Status = input.Status
                    FullTime = null
                    ExtraTime = null
                    Penalties = null
                    ResultPredictions = resultPredictions
                    QualificationPredictions = qualificationPredictions
                    ExternalId = input.ExternalId
                    Stage = stage
                    Version = md.AggregateSequenceNumber
                }

            do! Fixtures.insert fixtureModel
            do! updateFixtureOrder input.CompetitionId

            if stage <> "GROUP_STAGE" then
                do! Predictions.updateQualifiers (input.CompetitionId, "GROUP_STAGE", fixtureModel.HomeTeam.ExternalId, true)
                do! Predictions.updateQualifiers (input.CompetitionId, "GROUP_STAGE", fixtureModel.AwayTeam.ExternalId, true)

            if stage = "LAST_16" then
                let! numFixtures = Fixtures.getFixtureCount (input.CompetitionId, "LAST_16")
                if numFixtures = 8L then
                    do! updateQualifiedTeams competition

        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Fixtures.ScoreChanged (homeGoals, awayGoals) ->
        do! updateScore (md.AggregateId, md.AggregateSequenceNumber, { Home = homeGoals; Away = awayGoals }, None, None)

    | Fixtures.ScoreChanged2 { FullTime = fullTime; ExtraTime = extraTime; Penalties = penalties } ->
        do! updateScore (md.AggregateId, md.AggregateSequenceNumber, fullTime, extraTime, penalties)

    | Fixtures.StatusChanged status ->
        let! fixture = Fixtures.get md.AggregateId
        do! Fixtures.updateStatus (md.AggregateId, md.AggregateSequenceNumber) status
        let actualResult = mapActualResult (status.ToString(), fixture.FullTime, fixture.ExtraTime, fixture.Penalties)
        if fixture.Stage = "GROUP_STAGE" then
            if actualResult |> isNull |> not then
                do! Predictions.updateResult (fixture.CompetitionId, fixture.ExternalId, actualResult)
        else if status = Fixtures.Finished then
            do! Predictions.updateQualifiers (fixture.CompetitionId, fixture.Stage, fixture.HomeTeam.ExternalId, actualResult = "HomeWin")
            do! Predictions.updateQualifiers (fixture.CompetitionId, fixture.Stage, fixture.AwayTeam.ExternalId, actualResult = "AwayWin")
}


let processLeagues (log: ILogger) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Leagues.Event> (e.Event.EventType, e.Event.Data) with
    | Leagues.Created input ->
        try
            let leagueModel : ReadModels.League =
                {
                    Id = md.AggregateId
                    CompetitionId = input.CompetitionId
                    Code = input.Code
                    Name = input.Name
                }
            do! Leagues.insert leagueModel
        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Leagues.PredictionAdded predictionId ->
        do! Predictions.addToLeague (predictionId, md.AggregateId)
}


let eventAppeared (logger: ILogger, authOptions: AuthOptions) (subscription: PersistentSubscription) (e: ResolvedEvent) = unitTask {
    try
        logger.LogInformation($"Event %A{e.Event.EventId} of type %s{e.Event.EventType} appeared in stream %s{e.Event.EventStreamId}")
        match getMetadata e with
        | Some(md) when md.AggregateName = Competitions.AggregateName ->
            do! processCompetitions logger authOptions md e
        | Some(md) when md.AggregateName = Predictions.AggregateName ->
            do! processPredictions logger md e
        | Some(md) when md.AggregateName = Fixtures.AggregateName ->
            do! processFixtures logger md e
        | Some(md) when md.AggregateName = Leagues.AggregateName ->
            do! processLeagues logger md e
        | _ -> ()
        do! subscription.Ack(e)
        logger.LogInformation($"Event %A{e.Event.EventId} handled")
    with ex ->
        do! subscription.Nack(PersistentSubscriptionNakEventAction.Retry, "unexpected exception occured", e)
        logger.LogError(ex, $"Failed to handle event %A{e.Event.EventId}")
}


type private Marker = class end


let connectSubscription (client: EventStorePersistentSubscriptionsClient) (loggerFactory: ILoggerFactory) (authOptions: AuthOptions) (subscriptionsSettings: SubscriptionsSettings) = unitTask {
    let logger = loggerFactory.CreateLogger(typeof<Marker>.DeclaringType)
    logger.LogInformation("Initializing process manager")
    let! _ =
        client.SubscribeAsync(
            subscriptionsSettings.StreamName,
            subscriptionsSettings.GroupName,
            (fun sub e _ _ -> eventAppeared (logger, authOptions) sub e),
            autoAck = false
        )
    logger.LogInformation("Process manager initialized")
}

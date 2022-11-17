module FbApp.Api.ProcessManager


open EventStore.Client
open FbApp.Api.Configuration
open FbApp.Api.Domain
open FbApp.Api.EventStore
open FbApp.Api.Repositories
open FbApp.Api.Serialization
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open MongoDB.Driver
open System


module Result =
    let unwrap f = function
        | Ok(x) -> x
        | Error(x) -> failwith $"%A{f x}"


let (|Nullable|_|) (x: Nullable<_>) =
    if x.HasValue then Some(x.Value) else None


let upsertCompetition (logger: ILogger, db) (metadata: Metadata) (e: ResolvedEvent) (model: Competitions.Created) = task {
    try
        let competitionModel: ReadModels.Competition =
            {
                Id =  metadata.AggregateId
                Description = model.Description
                ExternalId = model.ExternalId
                Teams = [||]
                Fixtures = [||]
                Groups = dict []
                Players = [||]
                Version = metadata.AggregateSequenceNumber
                Date = model.Date.ToOffset(TimeSpan.Zero)
            }
        do! Competitions.insert db competitionModel
    with :? MongoWriteException as ex ->
        logger.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
}


let processCompetitions (logger: ILogger, db) (authOptions: AuthOptions) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Competitions.Event> (e.Event.EventType, e.Event.Data) with
    | Competitions.Created args ->
        try
            do! args |> upsertCompetition (logger, db) md e
            let! teams = FootballData.getCompetitionTeams authOptions.FootballDataToken args.ExternalId
            let teams = teams |> Result.unwrap (fun (_,_,err) -> failwith err.Error)
            let! fixtures = FootballData.getCompetitionFixtures authOptions.FootballDataToken args.ExternalId []
            let fixtures = fixtures |> Result.unwrap (fun (_,_,err) -> failwith err.Error)
            let! groups = FootballData.getCompetitionLeagueTable authOptions.FootballDataToken args.ExternalId
            let groups = groups |> Result.unwrap (fun (_,_,err) -> failwith err.Error)
            let command =
                Competitions.Command.AssignTeamsAndFixtures
                    (
                        teams.Teams |> Seq.map (fun x -> { Name = x.Name; Code = x.Code; FlagUrl = x.Crest; ExternalId = x.Id } : Competitions.TeamAssignment) |> Seq.toList,
                        fixtures.Fixtures |> Seq.filter (fun f -> f.Matchday |> Option.map ((>) 4) |> Option.defaultValue false) |> Seq.map (fun x -> { HomeTeamId = x.HomeTeam.Value.Id.Value; AwayTeamId = x.AwayTeam.Value.Id.Value; Date = x.Date; ExternalId = x.Id } : Competitions.FixtureAssignment) |> Seq.toList,
                        groups.Standings |> Seq.filter (fun r -> r.Stage = "GROUP_STAGE") |> Seq.map (fun kvp -> kvp.Group, (kvp.Table |> Array.map (fun x -> x.Team.Id))) |> Seq.toList,
                        teams.Teams |> Seq.map (fun t -> t.Players |> Seq.map (fun x -> { Name = x.Name; Position = x.Position; TeamExternalId = t.Id; ExternalId = x.Id } : Competitions.PlayerAssignment)) |> Seq.concat |> Seq.toList
                    )
            let id = Competitions.createId args.ExternalId
            let! _ = CommandHandlers.competitionsHandler (id, Aggregate.Version md.AggregateSequenceNumber) command
            ()
        with :? WrongExpectedVersionException as ex ->
            logger.LogInformation(ex, "Cannot process current event: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Competitions.PlayersAssigned players ->
        let playerProjections =
            players
            |> List.map
                (fun player ->
                    {
                        Name = player.Name
                        Position = player.Position
                        TeamExternalId = player.TeamExternalId
                        ExternalId = player.ExternalId
                    } : ReadModels.CompetitionPlayer)
            |> List.toArray
        do! Competitions.updatePlayers db (md.AggregateId, md.AggregateSequenceNumber) playerProjections
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
        do! Competitions.updateTeams db (md.AggregateId, md.AggregateSequenceNumber) teamProjections
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
        do! Competitions.updateFixtures db (md.AggregateId, md.AggregateSequenceNumber) fixtureProjections
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
        do! (dict groups) |> Competitions.updateGroups db (md.AggregateId, md.AggregateSequenceNumber)
}


let private mapActualResult (status, fullTime, extraTime: int array, penalties: int array) =
    match status, fullTime with
    | "FINISHED", [| homeGoals; awayGoals |] ->
        let et = if extraTime |> isNull then [| 0; 0 |] else extraTime
        let ps = if penalties |> isNull then [| 0; 0 |] else penalties
        let homeGoals = homeGoals + et[0] + ps[0]
        let awayGoals = awayGoals + et[1] + ps[1]
        if homeGoals > awayGoals then "HomeWin"
        elif homeGoals < awayGoals then "AwayWin"
        else "Tie"
    | _ -> null


let private mapFixtureResult db competitionId (x: Predictions.FixtureResultRegistration) = task {
    let mapResult = function
        | Predictions.HomeWin -> "HomeWin"
        | Predictions.Tie -> "Tie"
        | Predictions.AwayWin -> "AwayWin"

    let fixtureId = Fixtures.createId (competitionId, x.Id)
    let! result = Fixtures.getFixtureStatus db fixtureId

    let result = mapActualResult (result.Status, result.FullTime, result.ExtraTime, result.Penalties)

    let fixtureResult : ReadModels.PredictionFixtureResult =
        {
            FixtureId = x.Id
            PredictedResult = mapResult x.Result
            ActualResult = result
        }

    return fixtureResult
}


let acceptPrediction db (metadata: Metadata) (model: Predictions.PredictionRegistration) = task {
    let fixtureTasks =
        model.Fixtures
        |> Seq.map (mapFixtureResult db model.CompetitionId)
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
                do! Fixtures.addPrediction db id prediction
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
            TopScorers = model.TopScorers |> List.map (fun x -> { Id = x; IsCorrect = true } : ReadModels.ScorerResult) |> List.toArray
            Winner = { Id = model.Winner; HasQualified = Nullable() }
            Leagues = [||]
            Version = metadata.AggregateSequenceNumber
        }
    do! Predictions.insert db prediction
}


let processPredictions (logger: ILogger, db) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Predictions.Event> (e.Event.EventType, e.Event.Data) with
    | Predictions.Registered args ->
        let! competition = Competitions.get db args.CompetitionId
        if md.Timestamp > competition.Value.Date then
            let id = Predictions.createId (args.CompetitionId, Predictions.Email args.Email)
            let! _ = CommandHandlers.predictionsHandler (id, Aggregate.Any) Predictions.Decline
            ()
        else
            try
                do! acceptPrediction db md args
            with :? MongoWriteException as ex ->
                logger.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)
    | Predictions.Declined ->
        do! Predictions.delete db md.AggregateId
}


let updateFixtureOrder db competitionId = task {
    let! fixtures = Fixtures.getFixtureOrder db competitionId
    match fixtures |> Seq.toList with
    | [] | [_] -> ()
    | [x;y] ->
        if x.PreviousId.HasValue || x.NextId <> Nullable(y.Id) then
            do! Fixtures.setAdjacentFixtures db x.Id (None, Some(y.Id))
        if y.PreviousId <> Nullable(x.Id) || y.NextId.HasValue then
            do! Fixtures.setAdjacentFixtures db y.Id (Some(x.Id), None)
    | fixtures ->
        let x, y = match fixtures with x::y::_ -> x, y | _ -> failwith "never"
        if x.PreviousId.HasValue || x.NextId <> Nullable(y.Id) then
            do! Fixtures.setAdjacentFixtures db x.Id (None, Some(y.Id))
        for w in fixtures |> List.windowed 3 do
            let p, x, n = match w with [p; x; n] -> p, x, n | _ -> failwith "never"
            if x.PreviousId <> Nullable(p.Id) || x.NextId <> Nullable(n.Id) then
                do! Fixtures.setAdjacentFixtures db x.Id (Some(p.Id), Some(n.Id))
        let x, y = match fixtures |> List.rev with x::y::_ -> x, y | _ -> failwith "never"
        if x.PreviousId <> Nullable(y.Id) || x.NextId.HasValue then
            do! Fixtures.setAdjacentFixtures db x.Id (Some(y.Id), None)
}


let getResultPredictions db (input: Fixtures.AddFixtureInput) = task {
    if input.Stage <> "GROUP_STAGE" then return [||] else
    let! predictions = Predictions.ofFixture db input.CompetitionId input.ExternalId
    let predictions =
        predictions
        |> Seq.map
            (fun x ->
                {
                    PredictionId = x.Id
                    Name = x.Name
                    Result = x.Fixtures[0].PredictedResult
                } : ReadModels.FixtureResultPrediction)
        |> Seq.toArray
    return predictions
}


let getQualificationPredictions db (input: Fixtures.AddFixtureInput) = task {
    if input.Stage = "GROUP_STAGE" then return [||] else
    let! predictions = Predictions.ofStage db (input.CompetitionId, input.Stage)
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


let updateQualifiedTeams db (competition: ReadModels.Competition) = task {
    let! qualifiedTeams = Fixtures.getQualifiedTeams db competition.Id
    let unqualifiedTeams = competition.Teams |> Seq.map (fun x -> x.ExternalId) |> Seq.except qualifiedTeams |> Seq.toArray
    do! Predictions.setUnqualifiedTeams db (competition.Id, unqualifiedTeams)
}


let updateScore db (fixtureId: Guid, expectedVersion: int64, fullTime, extraTime, penalties) = task {
    let! fixture = Fixtures.get db fixtureId
    do! Fixtures.updateScore db (fixtureId, expectedVersion) (fullTime, extraTime, penalties)
    let ps = match penalties with Some(u) -> [| u.Home; u.Away |] | _ -> [| 0; 0 |]
    let et = match extraTime with Some(u) -> [| u.Home; u.Away |] | _ -> [| 0; 0 |]
    let actualResult = mapActualResult (fixture.Status, [| fullTime.Home; fullTime.Away |], et, ps)
    if fixture.Stage = "GROUP_STAGE" then
        if actualResult |> isNull |> not then
            do! Predictions.updateResult db (fixture.CompetitionId, fixture.ExternalId, actualResult)
    else if fixture.Status = "FINISHED" then
        do! Predictions.updateQualifiers db (fixture.CompetitionId, fixture.Stage, fixture.HomeTeam.ExternalId, actualResult = "HomeWin")
        do! Predictions.updateQualifiers db (fixture.CompetitionId, fixture.Stage, fixture.AwayTeam.ExternalId, actualResult = "AwayWin")
}


let processFixtures (log: ILogger, db) (md: Metadata) (e: ResolvedEvent) = task {
    match deserializeOf<Fixtures.Event> (e.Event.EventType, e.Event.Data) with
    | Fixtures.Added input ->
        try
            let! competition = Competitions.get db input.CompetitionId
            let competition = competition |> Option.get

            let homeTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.HomeTeamId)
            let awayTeam = competition.Teams |> Array.find (fun t -> t.ExternalId = input.AwayTeamId)

            let! resultPredictions = getResultPredictions db input
            let! qualificationPredictions = getQualificationPredictions db input

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

            do! Fixtures.insert db fixtureModel
            do! updateFixtureOrder db input.CompetitionId

            if stage <> "GROUP_STAGE" then
                do! Predictions.updateQualifiers db (input.CompetitionId, "GROUP_STAGE", fixtureModel.HomeTeam.ExternalId, true)
                do! Predictions.updateQualifiers db (input.CompetitionId, "GROUP_STAGE", fixtureModel.AwayTeam.ExternalId, true)

            if stage = "LAST_16" then
                let! numFixtures = Fixtures.getFixtureCount db (input.CompetitionId, "LAST_16")
                if numFixtures = 8L then
                    do! updateQualifiedTeams db competition

        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Fixtures.ScoreChanged (homeGoals, awayGoals) ->
        do! updateScore db (md.AggregateId, md.AggregateSequenceNumber, { Home = homeGoals; Away = awayGoals }, None, None)

    | Fixtures.ScoreChanged2 { FullTime = fullTime; ExtraTime = extraTime; Penalties = penalties } ->
        do! updateScore db (md.AggregateId, md.AggregateSequenceNumber, fullTime, extraTime, penalties)

    | Fixtures.StatusChanged status ->
        let! fixture = Fixtures.get db md.AggregateId
        do! Fixtures.updateStatus db (md.AggregateId, md.AggregateSequenceNumber) status
        let actualResult = mapActualResult (status.ToString(), fixture.FullTime, fixture.ExtraTime, fixture.Penalties)
        if fixture.Stage = "GROUP_STAGE" then
            if actualResult |> isNull |> not then
                do! Predictions.updateResult db (fixture.CompetitionId, fixture.ExternalId, actualResult)
        else if status = Fixtures.Finished then
            do! Predictions.updateQualifiers db (fixture.CompetitionId, fixture.Stage, fixture.HomeTeam.ExternalId, actualResult = "HomeWin")
            do! Predictions.updateQualifiers db (fixture.CompetitionId, fixture.Stage, fixture.AwayTeam.ExternalId, actualResult = "AwayWin")
}


let processLeagues (log: ILogger, db) (md: Metadata) (e: ResolvedEvent) = task {
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
            do! Leagues.insert db leagueModel
        with :? MongoWriteException as ex ->
            log.LogInformation(ex, "Already exists: {0} {1}", e.OriginalStreamId, e.OriginalEventNumber)

    | Leagues.PredictionAdded predictionId ->
        do! Predictions.addToLeague db (predictionId, md.AggregateId)
}


let eventAppeared (logger: ILogger, db, authOptions: AuthOptions) (subscription: PersistentSubscription) (e: ResolvedEvent) = task {
    try
        logger.LogInformation($"Event %A{e.Event.EventId} of type %s{e.Event.EventType} appeared in stream %s{e.Event.EventStreamId}")
        match getMetadata e with
        | Some(md) when md.AggregateName = Competitions.AggregateName ->
            do! processCompetitions (logger, db) authOptions md e
        | Some(md) when md.AggregateName = Predictions.AggregateName ->
            do! processPredictions (logger, db) md e
        | Some(md) when md.AggregateName = Fixtures.AggregateName ->
            do! processFixtures (logger, db) md e
        | Some(md) when md.AggregateName = Leagues.AggregateName ->
            do! processLeagues (logger, db) md e
        | _ -> ()
        do! subscription.Ack(e)
        logger.LogInformation($"Event %A{e.Event.EventId} handled")
    with ex ->
        do! subscription.Nack(PersistentSubscriptionNakEventAction.Retry, "unexpected exception occured", e)
        logger.LogError(ex, $"Failed to handle event %A{e.Event.EventId}")
}


type private Marker = class end


let (|Conflict|_|) (ex: exn) =
    match ex with
    | :? Grpc.Core.RpcException as e when e.StatusCode = Grpc.Core.StatusCode.Unknown && e.Status.Detail = "Envelope callback expected Updated, received Conflict instead" ->
        Some()
    | _ ->
        None


let (|AlreadyExists|_|) (ex: exn) =
    match ex with
    | :? Grpc.Core.RpcException as e when e.StatusCode = Grpc.Core.StatusCode.AlreadyExists ->
        Some()
    | _ ->
        None


let initProjections (services: IServiceProvider) = task {
    let logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof<Marker>.DeclaringType)
    let projectionManagementClient = services.GetRequiredService<EventStoreProjectionManagementClient>()
    let subscriptionsClient = services.GetRequiredService<EventStorePersistentSubscriptionsClient>()
    let subscriptionsSettings = services.GetRequiredService<IOptions<SubscriptionsSettings>>().Value

    let query = $"""fromAll()
.when({{
    $any: function (state, ev) {{
        if (ev.metadata !== null && ev.metadata.applicationName === "%s{ApplicationName}") {{
            linkTo("%s{subscriptionsSettings.StreamName}", ev)
        }}
    }}
}})"""

    try
        logger.LogInformation($"Trying to create '%s{subscriptionsSettings.StreamName}' projection (if not exists)")
        do! projectionManagementClient.CreateContinuousAsync(subscriptionsSettings.StreamName, query)
        do! projectionManagementClient.UpdateAsync(subscriptionsSettings.StreamName, query, emitEnabled = true)
    with
    | Conflict ->
        ()
    | e ->
        logger.LogCritical(e, $"Error occurred while initializing '%s{subscriptionsSettings.StreamName}' projection")
        raise e

    let settings =
        PersistentSubscriptionSettings(
            resolveLinkTos = true,
            startFrom = StreamPosition.Start,
            checkPointLowerBound = 1
        )

    try
        logger.LogInformation($"Trying to create '%s{subscriptionsSettings.GroupName}' subscription group ...")
        do! subscriptionsClient.CreateAsync(subscriptionsSettings.StreamName, subscriptionsSettings.GroupName, settings)
    with
    | AlreadyExists ->
        logger.LogInformation($"Subscription group '%s{subscriptionsSettings.GroupName}' already exists")
    | e ->
        logger.LogCritical(e, $"Error occurred while initializing '%s{subscriptionsSettings.GroupName}' subscription group")
        raise e
}


let connectSubscription (services: IServiceProvider) = task {
    do! initProjections services

    let logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof<Marker>.DeclaringType)
    let client = services.GetRequiredService<EventStorePersistentSubscriptionsClient>()
    let subscriptionsSettings = services.GetRequiredService<IOptions<SubscriptionsSettings>>().Value
    let mongoDb = services.GetRequiredService<IMongoDatabase>()
    let authOptions = services.GetService<IOptions<AuthOptions>>().Value

    logger.LogInformation("Initializing process manager")
    let! _ =
        client.SubscribeToStreamAsync(
            subscriptionsSettings.StreamName,
            subscriptionsSettings.GroupName,
            (fun sub e _ _ -> eventAppeared (logger, mongoDb, authOptions) sub e)
        )
    logger.LogInformation("Process manager initialized")
}

module FbApp.PredictionResults.ReadModel

open System
open System.Collections.Generic
open EventStore.Client
open FbApp.Api
open FbApp.Api.Configuration
open FbApp.Api.Domain
open FbApp.Api.EventStore
open FbApp.Common.SimpleTypes
open Microsoft.Extensions.Logging

let MaxTotalPoints
    = 8 * 6 * 1 // Group matches
    + 16 * 2 // Qualifiers
    + 8 * 3 // Quarter-finalists
    + 4 * 4 // Semi-finalists
    + 2 * 5 // Finalists
    + 1 * 6 // Winner
    + 1 * 5 // Top scorer

type Fixture (fixtureId: FixtureId, homeTeamId: TeamId, awayTeamId: TeamId, fixtureStatus: Fixtures.FixtureStatus, fixtureStage: Fixtures.FixtureStage, version: int64) =
    member val FixtureId = fixtureId with get
    member val HomeTeamId = homeTeamId with get
    member val AwayTeamId = awayTeamId with get
    member val FixtureResult = Predictions.FixtureResult.Tie with get, set
    member val FixtureStatus = fixtureStatus with get, set
    member val FixtureStage = fixtureStage with get
    member val Version = version with get, set

type Predictions = {
    Matches: IDictionary<FixtureId, Predictions.FixtureResult>
    Qualifiers: TeamId list
    QuarterFinals: TeamId list
    SemiFinals: TeamId list
    Finals: TeamId list
    Winner: TeamId
    TopScorers: PlayerId list
}

module Dict =
    let tryGet (key: 'Key) (values: #IDictionary<'Key, 'Value>) =
        match values.TryGetValue(key) with
        | true, value -> Some(value)
        | _ -> None

    let tryUpdate (key: 'Key) (getNewValue: 'Value -> 'Value) (values: #IDictionary<'Key, 'Value>) =
        match values.TryGetValue(key) with
        | true, value -> values[key] <- getNewValue(value)
        | _ -> ()

type PredictionStatus =
    | Final of bool
    | Pending of bool
    | Unavailable

type CompetitionScorer = {
    PlayerId: PlayerId
    GoalCount: int
    IsTopScorer: PredictionStatus
}

type Scoresheet(predictionId: PredictionId, name: string, predictions: Predictions) =
    let mutable scorers: CompetitionScorer list  = []
    member _.Scorer with get() = scorers
    member val GroupStage =
        predictions.Matches
            |> Seq.map (fun kvp -> KeyValuePair<_, bool option>(kvp.Key, None))
            |> Dictionary<_,_>
        with get
    member val Qualifiers =
        predictions.Qualifiers
            |> Seq.map (fun id -> KeyValuePair<_, bool option>(id, None))
            |> Dictionary<_,_>
        with get
    member val Quarters =
        predictions.QuarterFinals
            |> Seq.map (fun id -> KeyValuePair<_, bool option>(id, None))
            |> Dictionary<_,_>
        with get
    member val Semis =
        predictions.SemiFinals
            |> Seq.map (fun id -> KeyValuePair<_, bool option>(id, None))
            |> Dictionary<_,_>
        with get
    member val Final =
        predictions.Finals
            |> Seq.map (fun id -> KeyValuePair<_, bool option>(id, None))
            |> Dictionary<_,_>
        with get
    member val Winner: TeamId * bool option = (predictions.Winner, None) with get, set
    member val Predictions = predictions with get
    member val PredictionId = predictionId with get
    member val Name = name with get
    member this.UpdateQualifers(qualified: TeamId list, unqualified: TeamId list) =
        qualified |> List.iter (fun teamId -> if this.Qualifiers.ContainsKey(teamId) then this.Qualifiers[teamId] <- Some true else ())
        unqualified |> List.iter (fun teamId -> if this.Qualifiers.ContainsKey(teamId) then this.Qualifiers[teamId] <- Some false else ())
        this.UpdateQuarters([], unqualified)
    member this.UpdateQuarters(qualified: TeamId list, unqualified: TeamId list) =
        qualified |> List.iter (fun teamId -> if this.Quarters.ContainsKey(teamId) then this.Quarters[teamId] <- Some true else ())
        unqualified |> List.iter (fun teamId -> if this.Quarters.ContainsKey(teamId) then this.Quarters[teamId] <- Some false else ())
        this.UpdateSemis([], unqualified)
    member this.UpdateSemis(qualified: TeamId list, unqualified: TeamId list) =
        qualified |> List.iter (fun teamId -> if this.Semis.ContainsKey(teamId) then this.Semis[teamId] <- Some true else ())
        unqualified |> List.iter (fun teamId -> if this.Semis.ContainsKey(teamId) then this.Semis[teamId] <- Some false else ())
        this.UpdateFinal([], unqualified)
    member this.UpdateFinal(qualified: TeamId list, unqualified: TeamId list) =
        qualified |> List.iter (fun teamId -> if this.Final.ContainsKey(teamId) then this.Final[teamId] <- Some true else ())
        unqualified |> List.iter (fun teamId -> if this.Final.ContainsKey(teamId) then this.Final[teamId] <- Some false else ())
        this.UpdateWinner([], unqualified)
    member this.UpdateWinner(qualified: TeamId list, unqualified: TeamId list) =
        qualified |> List.iter (fun teamId -> if fst this.Winner = teamId then this.Winner <- (teamId, Some true) else ())
        unqualified |> List.iter (fun teamId -> if fst this.Winner = teamId then this.Winner <- (teamId, Some false) else ())
    member this.SetFixtureResult(fixtureId: FixtureId, fixtureResult: Predictions.FixtureResult) =
        this.GroupStage
            |> Dict.tryUpdate fixtureId (fun _ ->
                predictions.Matches
                    |> Dict.tryGet fixtureId
                    |> Option.map (fun predictedResult -> Some (predictedResult = fixtureResult))
                    |> Option.defaultValue (Some false))
    member this.UpdateScorers(competitionScorers: Map<PlayerId, CompetitionScorer>) =
        scorers <-
            predictions.TopScorers
            |> List.choose (fun id -> competitionScorers |> Map.tryFind id)

// TODO : Concurrent collections !?
module InMemoryStore =
    let fixtures = Dictionary<FixtureId, Fixture>()
    let qualifiedTeams = (HashSet<TeamId>(), HashSet<TeamId>())
    let quarterFinalTeams = (HashSet<TeamId>(), HashSet<TeamId>())
    let semiFinalTeams = (HashSet<TeamId>(), HashSet<TeamId>())
    let finalTeams = (HashSet<TeamId>(), HashSet<TeamId>())
    let winner = (HashSet<TeamId>(), HashSet<TeamId>())
    let scoresheets = Dictionary<PredictionId, Scoresheet>()

let getMetadata (e: ResolvedEvent) : Metadata option =
    e.Event
    |> Option.ofObj
    |> Option.bind (fun x ->
        match x.Metadata with
        | v when v.IsEmpty -> None
        | arr -> Some(Serialization.deserializeType arr)
    )

let fixtureResultFromScore (args: Fixtures.ScoreChangedArgs) =
    let home =
        args.FullTime.Home
            + (args.ExtraTime |> Option.map (fun x -> x.Home) |> Option.defaultValue 0)
            + (args.Penalties |> Option.map (fun x -> x.Home) |> Option.defaultValue 0)
    let away =
        args.FullTime.Away
            + (args.ExtraTime |> Option.map (fun x -> x.Away) |> Option.defaultValue 0)
            + (args.Penalties |> Option.map (fun x -> x.Away) |> Option.defaultValue 0)
    if home > away then
        Predictions.FixtureResult.HomeWin
    else if home < away then
        Predictions.FixtureResult.AwayWin
    else
        Predictions.FixtureResult.Tie

let isQualified (group: Competitions.StandingRow list) (team: Competitions.StandingRow) =
    let pts (t: Competitions.StandingRow) =
        3 * t.Won + t.Draw
    let teamPoints = pts team
    group
    |> List.filter ((<>) team)
    |> List.filter (fun x ->
        let maxPoints = pts x + 3 * (3 - x.PlayedGames)
        maxPoints >= teamPoints
    )
    |> List.length
    |> ((>) 2)

let isUnqualified thirdMayAdvance (group: Competitions.StandingRow list) (team: Competitions.StandingRow) =
    let pts (t: Competitions.StandingRow) =
        3 * t.Won + t.Draw + 3 * (3 - t.PlayedGames)
    let teamPoints = pts team
    let decider =
        if thirdMayAdvance then
            ((<) 3)
        else
            ((<) 2)
    group
    |> List.filter ((<>) team)
    |> List.filter (fun x ->
        let maxPoints = pts x
        maxPoints > teamPoints
    )
    |> List.length
    |> decider

let selectCorrect (correct: HashSet<TeamId>, _) (teams: TeamId seq) =
    correct
        |> Set.ofSeq
        |> Set.intersect (Set.ofSeq teams)
        |> Set.toSeq

let selectWrong (_, wrong: HashSet<TeamId>) (teams: TeamId seq) =
    wrong
        |> Set.ofSeq
        |> Set.intersect (Set.ofSeq teams)
        |> Set.toSeq

let updateQualifiedTeams thirdMayAdvance (rows: Competitions.StandingRow list) =
    let correct, wrong = InMemoryStore.qualifiedTeams
    rows
        |> List.map (fun x -> TeamId.create x.TeamId)
        |> List.iter (fun x -> correct.Remove(x) |> ignore; wrong.Remove(x) |> ignore)
    if rows |> List.exists (fun x -> x.PlayedGames < 3) then
        rows |> List.filter (isQualified rows) |> List.map (fun x -> TeamId.create x.TeamId) |> List.iter (correct.Add >> ignore)
        rows |> List.filter (isUnqualified thirdMayAdvance rows) |>  List.map (fun x -> TeamId.create x.TeamId) |> List.iter (wrong.Add >> ignore)
    else
        rows |> List.filter (fun x -> x.Position < 3) |> List.map (fun x -> TeamId.create x.TeamId) |> List.iter (correct.Add >> ignore)
        let decider: Competitions.StandingRow -> bool = if thirdMayAdvance then (fun x -> x.Position > 3) else (fun x -> x.Position > 2)
        rows |> List.filter decider |>  List.map (fun x -> TeamId.create x.TeamId) |> List.iter (wrong.Add >> ignore)
    wrong |> Seq.iter (fun x -> (snd InMemoryStore.quarterFinalTeams).Add(x) |> ignore)
    wrong |> Seq.iter (fun x -> (snd InMemoryStore.semiFinalTeams).Add(x) |> ignore)
    wrong |> Seq.iter (fun x -> (snd InMemoryStore.finalTeams).Add(x) |> ignore)
    wrong |> Seq.iter (fun x -> (snd InMemoryStore.winner).Add(x) |> ignore)

let getDropoutTeams () = [
    yield! snd InMemoryStore.winner
    yield! snd InMemoryStore.finalTeams
    yield! snd InMemoryStore.semiFinalTeams
    yield! snd InMemoryStore.quarterFinalTeams
    yield! snd InMemoryStore.qualifiedTeams
]

let processCompetitions _ _ = function
    | Competitions.Event.StandingsUpdated (_, rows) ->
        updateQualifiedTeams (FootballData.ActiveCompetition = FootballData.EuropeanChampionship) rows
        InMemoryStore.scoresheets.Values
            |> Seq.iter (fun scoresheet ->
                let qualified = fst InMemoryStore.qualifiedTeams |> Seq.toList
                let unqualified = snd InMemoryStore.qualifiedTeams |> Seq.toList
                scoresheet.UpdateQualifers(qualified, unqualified)
            )
    | Competitions.Event.ScorersUpdated scorers ->
        let isFinal = (fst InMemoryStore.winner |> Seq.length) > 0
        let dropoutTeams = getDropoutTeams ()
        let maxGoals = if scorers |> List.isEmpty then None else Some (scorers |> List.map (fun x -> x.Goals) |> List.max)
        let competitionScorers: Map<PlayerId, CompetitionScorer> =
            scorers
            |> List.map (fun x ->
                let data = {
                    CompetitionScorer.PlayerId = PlayerId.create x.PlayerId
                    GoalCount = x.Goals
                    IsTopScorer =
                        if isFinal then
                            Final(Some x.Goals = maxGoals)
                        else if dropoutTeams |> List.contains (TeamId.create x.TeamId) then
                            if Some x.Goals <> maxGoals then
                                Final(false)
                            else
                                Pending(Some x.Goals = maxGoals)
                        else
                            Pending(Some x.Goals = maxGoals)
                }
                (data.PlayerId, data))
            |> Map.ofList
        InMemoryStore.scoresheets.Values
            |> Seq.iter (fun scoreSheet -> scoreSheet.UpdateScorers(competitionScorers))
    | Competitions.Event.Created _
    | Competitions.Event.FixturesAssigned _
    | Competitions.Event.GroupsAssigned _
    | Competitions.Event.PlayersAssigned _
    | Competitions.Event.TeamsAssigned _ ->
        ()

let removeTeamsFrom (arr: ResizeArray<TeamId>) (tms: TeamId list) =
    tms |> List.iter (arr.Remove >> ignore)

let processFixtures (aggregateId: Guid) _ (version: int64) = function
    | Fixtures.Added args ->
        let competitionId = CompetitionId.fromGuid args.CompetitionId
        Fixtures.FixtureStage.tryFromString args.Stage
            |> Option.map (fun fixtureStage ->
                Fixture(
                    FixtureId.create competitionId args.ExternalId,
                    TeamId.create args.HomeTeamId,
                    TeamId.create args.AwayTeamId,
                    Fixtures.FixtureStatus.FromString args.Status,
                    fixtureStage,
                    version
                )
            )
            |> Option.iter (fun fixture -> InMemoryStore.fixtures.Add(fixture.FixtureId, fixture))
    | Fixtures.ScoreChanged2 args ->
        let fixtureId = FixtureId.fromGuid aggregateId
        let fixtureResult = fixtureResultFromScore args
        InMemoryStore.fixtures
        |> Dict.tryGet fixtureId
        |> Option.iter (fun fixture ->
            fixture.FixtureResult <- fixtureResult
            match fixture.FixtureStatus with
            | Fixtures.FixtureStatus.Finished ->
                InMemoryStore.scoresheets.Values
                    |> Seq.iter (fun scoresheet ->
                        match fixture.FixtureStage with
                        | Fixtures.Group ->
                            scoresheet.SetFixtureResult(fixtureId, fixture.FixtureResult)
                        | Fixtures.Last16 ->
                            match fixtureResult with
                            | Predictions.HomeWin ->
                                scoresheet.UpdateQuarters([fixture.HomeTeamId], [fixture.AwayTeamId])
                            | Predictions.AwayWin ->
                                scoresheet.UpdateQuarters([fixture.AwayTeamId], [fixture.HomeTeamId])
                            | Predictions.Tie ->
                                scoresheet.UpdateQuarters([], [fixture.HomeTeamId; fixture.AwayTeamId])
                        | Fixtures.QuarterFinals ->
                            match fixtureResult with
                            | Predictions.HomeWin ->
                                scoresheet.UpdateSemis([fixture.HomeTeamId], [fixture.AwayTeamId])
                            | Predictions.AwayWin ->
                                scoresheet.UpdateSemis([fixture.AwayTeamId], [fixture.HomeTeamId])
                            | Predictions.Tie ->
                                scoresheet.UpdateSemis([], [fixture.HomeTeamId; fixture.AwayTeamId])
                        | Fixtures.SemiFinals ->
                            match fixture.FixtureResult with
                            | Predictions.HomeWin ->
                                scoresheet.UpdateFinal([fixture.HomeTeamId], [fixture.AwayTeamId])
                            | Predictions.AwayWin ->
                                scoresheet.UpdateFinal([fixture.AwayTeamId], [fixture.HomeTeamId])
                            | Predictions.Tie ->
                                scoresheet.UpdateFinal([], [fixture.HomeTeamId; fixture.AwayTeamId])
                        | Fixtures.Final ->
                            match fixture.FixtureResult with
                            | Predictions.HomeWin ->
                                scoresheet.UpdateWinner([fixture.HomeTeamId], [fixture.AwayTeamId])
                            | Predictions.AwayWin ->
                                scoresheet.UpdateWinner([fixture.AwayTeamId], [fixture.HomeTeamId])
                            | Predictions.Tie ->
                                scoresheet.UpdateWinner([], [fixture.HomeTeamId; fixture.AwayTeamId])
                        | _ -> ()
                    )
            | _ -> ()
        )
    | Fixtures.StatusChanged fixtureStatus ->
        let fixtureId = FixtureId.fromGuid aggregateId
        InMemoryStore.fixtures
        |> Dict.tryGet fixtureId
        |> Option.iter (fun fixture ->
            fixture.FixtureStatus <- fixtureStatus
            match fixtureStatus with
            | Fixtures.FixtureStatus.Finished ->
                InMemoryStore.scoresheets.Values
                    |> Seq.iter (fun scoresheet ->
                        match fixture.FixtureStage with
                        | Fixtures.Group ->
                            scoresheet.SetFixtureResult(fixtureId, fixture.FixtureResult)
                        | Fixtures.Last16 ->
                            match fixture.FixtureResult with
                            | Predictions.HomeWin ->
                                scoresheet.UpdateQuarters([fixture.HomeTeamId], [fixture.AwayTeamId])
                            | Predictions.AwayWin ->
                                scoresheet.UpdateQuarters([fixture.AwayTeamId], [fixture.HomeTeamId])
                            | Predictions.Tie ->
                                scoresheet.UpdateQuarters([], [fixture.HomeTeamId; fixture.AwayTeamId])
                        | Fixtures.QuarterFinals ->
                            match fixture.FixtureResult with
                            | Predictions.HomeWin ->
                                scoresheet.UpdateSemis([fixture.HomeTeamId], [fixture.AwayTeamId])
                            | Predictions.AwayWin ->
                                scoresheet.UpdateSemis([fixture.AwayTeamId], [fixture.HomeTeamId])
                            | Predictions.Tie ->
                                scoresheet.UpdateSemis([], [fixture.HomeTeamId; fixture.AwayTeamId])
                        | Fixtures.SemiFinals ->
                            match fixture.FixtureResult with
                            | Predictions.HomeWin ->
                                scoresheet.UpdateFinal([fixture.HomeTeamId], [fixture.AwayTeamId])
                            | Predictions.AwayWin ->
                                scoresheet.UpdateFinal([fixture.AwayTeamId], [fixture.HomeTeamId])
                            | Predictions.Tie ->
                                scoresheet.UpdateFinal([], [fixture.HomeTeamId; fixture.AwayTeamId])
                        | Fixtures.Final ->
                            match fixture.FixtureResult with
                            | Predictions.HomeWin ->
                                scoresheet.UpdateWinner([fixture.HomeTeamId], [fixture.AwayTeamId])
                            | Predictions.AwayWin ->
                                scoresheet.UpdateWinner([fixture.AwayTeamId], [fixture.HomeTeamId])
                            | Predictions.Tie ->
                                scoresheet.UpdateWinner([], [fixture.HomeTeamId; fixture.AwayTeamId])
                        | _ -> ()
                    )
            | _ -> ()
        )

let processPredictions _ _ = function
    | Predictions.Registered args ->
        let competitionId = CompetitionId.fromGuid args.CompetitionId
        let predictionId = PredictionId.create competitionId (Predictions.Email args.Email)
        let predictions =
            {
                Predictions.Matches =
                    args.Fixtures
                        |> List.map (fun x -> (FixtureId.create competitionId x.Id, x.Result))
                        |> dict
                Qualifiers = args.Qualifiers.RoundOf16 |> List.map TeamId.create
                QuarterFinals = args.Qualifiers.RoundOf8 |> List.map TeamId.create
                SemiFinals = args.Qualifiers.RoundOf4 |> List.map TeamId.create
                Finals = args.Qualifiers.RoundOf2 |> List.map TeamId.create
                Winner = TeamId.create args.Winner
                TopScorers = args.TopScorers |> List.map PlayerId.create
            }
        let scoresheet = Scoresheet(predictionId, args.Name, predictions)
        InMemoryStore.scoresheets.Add(predictionId, scoresheet)
    | Predictions.Declined ->
        ()

let registerPredictionResultHandlers (logger: ILogger) (client: EventStoreClient) (subscriptionSettings: SubscriptionsSettings) =
    let mutable checkpoint = StreamPosition.Start

    let processEvents (e: ResolvedEvent) (handler: ILogger -> int64 -> 'Event -> unit) =
        let event = Serialization.deserializeOf<'Event> (e.Event.EventType, e.Event.Data)
        handler logger (e.OriginalEventNumber.ToInt64()) event

    let eventAppeared (e: ResolvedEvent) = task {
        try
            match getMetadata e with
            | Some(md) when md.AggregateName = Competitions.AggregateName ->
                processEvents e processCompetitions
            | Some(md) when md.AggregateName = Fixtures.AggregateName ->
                processEvents e (processFixtures md.AggregateId)
            | Some(md) when md.AggregateName = Predictions.AggregateName ->
                processEvents e processPredictions
            | _ ->
                ()
            checkpoint <- e.OriginalEventNumber
        with ex ->
            logger.LogError(ex, $"Failed to handle event %A{e.Event.EventId}")
            raise ex
    }

    logger.LogInformation("Initializing prediction results handler")

    let rec subscribe () = task {
        let! _ =
            client.SubscribeToStreamAsync(
                subscriptionSettings.StreamName,
                FromStream.After(checkpoint),
                (fun sub e _ -> eventAppeared e),
                true,
                subscriptionDropped = (fun _ _ _ ->
                    logger.LogInformation("Trying to reconnect")
                    subscribe().Wait()
                )
            )
        ()
    }

    subscribe().Wait()

    logger.LogInformation("Prediction results handler initialized")

let getPredictionResults () =
    InMemoryStore.scoresheets.Values
        |> Seq.toList

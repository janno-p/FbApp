module FbApp.PredictionResults.ReadModel

open System
open System.Collections.Generic
open System.Text
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

type Fixture (fixtureId: FixtureId, fixtureStatus: Fixtures.FixtureStatus, fixtureStage: string, version: int64) =
    member val FixtureId = fixtureId with get
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

type DetailedScore () =
    member val Matches = ResizeArray<FixtureId>() with get
    member val Qualifiers = ResizeArray<TeamId>() with get
    member val QuarterFinals = ResizeArray<TeamId>() with get
    member val SemiFinals = ResizeArray<TeamId>() with get
    member val Finals = ResizeArray<TeamId>() with get
    member val Winner: TeamId option = None with get, set
    member val TopScorer: PlayerId option = None with get, set
    override this.ToString() =
        Encoding.UTF8.GetString((Serialization.serialize this |> snd).ToArray())

type PredictionResult = {
    PredictionId: PredictionId
    Name: string
    Predictions: Predictions
    GainedPoints: DetailedScore
    PendingPoints: DetailedScore
    LostPoints: DetailedScore
    TotalPoints: int32
    Version: int64
}

module Dict =
    let tryGet (key: 'Key) (values: Dictionary<'Key, 'Value>) =
        match values.TryGetValue(key) with
        | true, value -> Some(value)
        | _ -> None

module PredictionResult =
    let create (predictionId: PredictionId) (name: string) (predictions: Predictions) (version: int64) =
        {
            PredictionId = predictionId
            Name = name
            Predictions = predictions
            GainedPoints = DetailedScore()
            PendingPoints = DetailedScore()
            LostPoints = DetailedScore()
            TotalPoints = 0
            Version = version
        }

// TODO : Concurrent collections !?
module InMemoryStore =
    let fixtures = Dictionary<FixtureId, Fixture>()
    let qualifiedTeams = (HashSet<TeamId>(), HashSet<TeamId>())
    let quarterFinalTeams = (HashSet<TeamId>(), HashSet<TeamId>())
    let semiFinalTeams = (HashSet<TeamId>(), HashSet<TeamId>())
    let finalTeams = (HashSet<TeamId>(), HashSet<TeamId>())
    let winner = (HashSet<TeamId>(), HashSet<TeamId>())
    let predictionResults = Dictionary<PredictionId, PredictionResult>()

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

let isUnqualified (group: Competitions.StandingRow list) (team: Competitions.StandingRow) =
    let pts (t: Competitions.StandingRow) =
        3 * t.Won + t.Draw + 3 * (3 - t.PlayedGames)
    let teamPoints = pts team
    group
    |> List.filter ((<>) team)
    |> List.filter (fun x ->
        let maxPoints = pts x
        maxPoints > teamPoints
    )
    |> List.length
    |> ((<) 2)

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

let updateQualifiedTeams (rows: Competitions.StandingRow list) =
    let correct, wrong = InMemoryStore.qualifiedTeams
    rows
        |> List.map (fun x -> TeamId.create x.TeamId)
        |> List.iter (fun x -> correct.Remove(x) |> ignore; wrong.Remove(x) |> ignore)
    if rows |> List.exists (fun x -> x.PlayedGames < 3) then
        rows |> List.filter (isQualified rows) |> List.map (fun x -> TeamId.create x.TeamId) |> List.iter (correct.Add >> ignore)
        rows |> List.filter (isUnqualified rows) |>  List.map (fun x -> TeamId.create x.TeamId) |> List.iter (wrong.Add >> ignore)
    else
        rows |> List.filter (fun x -> x.Position < 3) |> List.map (fun x -> TeamId.create x.TeamId) |> List.iter (correct.Add >> ignore)
        rows |> List.filter (fun x -> x.Position > 2) |>  List.map (fun x -> TeamId.create x.TeamId) |> List.iter (wrong.Add >> ignore)
    wrong |> Seq.iter (fun x -> (snd InMemoryStore.quarterFinalTeams).Add(x) |> ignore)
    wrong |> Seq.iter (fun x -> (snd InMemoryStore.semiFinalTeams).Add(x) |> ignore)
    wrong |> Seq.iter (fun x -> (snd InMemoryStore.finalTeams).Add(x) |> ignore)
    wrong |> Seq.iter (fun x -> (snd InMemoryStore.winner).Add(x) |> ignore)

let processCompetitions _ _ = function
    | Competitions.Event.StandingsUpdated (_, rows) ->
        updateQualifiedTeams rows
        InMemoryStore.predictionResults.Values
            |> Seq.iter (fun prediction ->
                prediction.PendingPoints.Qualifiers.Clear()
                prediction.GainedPoints.Qualifiers.Clear()
                prediction.GainedPoints.Qualifiers.AddRange(selectCorrect InMemoryStore.qualifiedTeams prediction.Predictions.Qualifiers)
                prediction.LostPoints.Qualifiers.Clear()
                prediction.LostPoints.Qualifiers.AddRange(selectWrong InMemoryStore.qualifiedTeams prediction.Predictions.Qualifiers)
            )
    | Competitions.Event.Created _
    | Competitions.Event.FixturesAssigned _
    | Competitions.Event.GroupsAssigned _
    | Competitions.Event.PlayersAssigned _
    | Competitions.Event.TeamsAssigned _ ->
        ()

let processFixtures (aggregateId: Guid) _ (version: int64) = function
    | Fixtures.Added args ->
        let competitionId = CompetitionId.fromGuid args.CompetitionId
        let fixture =
            Fixture(
                FixtureId.create competitionId args.ExternalId,
                Fixtures.FixtureStatus.FromString args.Status,
                args.Stage,
                version
            )
        InMemoryStore.fixtures.Add(fixture.FixtureId, fixture)
    | Fixtures.ScoreChanged2 args ->
        let fixtureId = FixtureId.fromGuid aggregateId
        let fixtureResult = fixtureResultFromScore args
        InMemoryStore.fixtures
        |> Dict.tryGet fixtureId
        |> Option.iter (fun fixture ->
            fixture.FixtureResult <- fixtureResult
            match fixture.FixtureStatus with
            | Fixtures.FixtureStatus.Finished ->
                InMemoryStore.predictionResults.Values
                    |> Seq.iter (fun prediction ->
                        let predictionResult = prediction.Predictions.Matches[fixtureId]
                        match fixture.FixtureStage with
                        | "GROUP_STAGE" ->
                            prediction.PendingPoints.Matches.Remove(fixtureId) |> ignore
                            prediction.GainedPoints.Matches.Remove(fixtureId) |> ignore
                            prediction.LostPoints.Matches.Remove(fixtureId) |> ignore
                            if fixtureResult = predictionResult then
                                prediction.GainedPoints.Matches.Add(fixtureId)
                            else
                                prediction.LostPoints.Matches.Add(fixtureId)
                        | "LAST_16" ->
                            ()
                        | "QUARTER_FINALS" ->
                            ()
                        | "SEMI_FINALS" ->
                            ()
                        | "FINAL" ->
                            ()
                        | _ ->
                            ()
                    )
            | _ ->
                InMemoryStore.predictionResults.Values
                    |> Seq.iter (fun prediction ->
                        let predictionResult = prediction.Predictions.Matches[fixtureId]
                        match fixture.FixtureStage with
                        | "GROUP_STAGE" ->
                            prediction.PendingPoints.Matches.Remove(fixtureId) |> ignore
                            prediction.GainedPoints.Matches.Remove(fixtureId) |> ignore
                            prediction.LostPoints.Matches.Remove(fixtureId) |> ignore
                            if fixtureResult = predictionResult then
                                prediction.PendingPoints.Matches.Add(fixtureId)
                        | "LAST_16" ->
                            ()
                        | "QUARTER_FINALS" ->
                            ()
                        | "SEMI_FINALS" ->
                            ()
                        | "FINAL" ->
                            ()
                        | _ ->
                            ()
                    )
        )
    | Fixtures.StatusChanged fixtureStatus ->
        let fixtureId = FixtureId.fromGuid aggregateId
        InMemoryStore.fixtures
        |> Dict.tryGet fixtureId
        |> Option.iter (fun fixture ->
            fixture.FixtureStatus <- fixtureStatus
            match fixtureStatus with
            | Fixtures.FixtureStatus.InPlay
            | Fixtures.FixtureStatus.Paused ->
                InMemoryStore.predictionResults.Values
                    |> Seq.iter (fun prediction ->
                        let predictionResult = prediction.Predictions.Matches[fixtureId]
                        match fixture.FixtureStage with
                        | "GROUP_STAGE" ->
                            prediction.PendingPoints.Matches.Remove(fixtureId) |> ignore
                            prediction.GainedPoints.Matches.Remove(fixtureId) |> ignore
                            prediction.LostPoints.Matches.Remove(fixtureId) |> ignore
                            if fixture.FixtureResult = predictionResult then
                                prediction.PendingPoints.Matches.Add(fixtureId)
                        | "LAST_16" ->
                            ()
                        | "QUARTER_FINALS" ->
                            ()
                        | "SEMI_FINALS" ->
                            ()
                        | "FINAL" ->
                            ()
                        | _ ->
                            ()
                    )
            | Fixtures.FixtureStatus.Finished ->
                InMemoryStore.predictionResults.Values
                    |> Seq.iter (fun prediction ->
                        let predictionResult = prediction.Predictions.Matches[fixtureId]
                        match fixture.FixtureStage with
                        | "GROUP_STAGE" ->
                            prediction.PendingPoints.Matches.Remove(fixtureId) |> ignore
                            prediction.GainedPoints.Matches.Remove(fixtureId) |> ignore
                            prediction.LostPoints.Matches.Remove(fixtureId) |> ignore
                            if fixture.FixtureResult = predictionResult then
                                prediction.GainedPoints.Matches.Add(fixtureId)
                            else
                                prediction.LostPoints.Matches.Add(fixtureId)
                        | "LAST_16" ->
                            ()
                        | "QUARTER_FINALS" ->
                            ()
                        | "SEMI_FINALS" ->
                            ()
                        | "FINAL" ->
                            ()
                        | _ ->
                            ()
                    )
            | _ -> ()
        )

let processPredictions _ (version: int64) = function
    | Predictions.Registered args ->
        let competitionId = CompetitionId.fromGuid args.CompetitionId
        let predictionId = PredictionId.create competitionId (Predictions.Email args.Email)
        let fixResult = function
            | Predictions.FixtureResult.AwayWin ->
                Predictions.FixtureResult.HomeWin
            | Predictions.FixtureResult.HomeWin ->
                Predictions.FixtureResult.AwayWin
            | Predictions.FixtureResult.Tie ->
                Predictions.FixtureResult.Tie
        let predictions =
            {
                Predictions.Matches =
                    args.Fixtures
                        |> List.map (fun x -> (FixtureId.create competitionId x.Id, fixResult x.Result))
                        |> dict
                Qualifiers = args.Qualifiers.RoundOf16 |> List.map TeamId.create
                QuarterFinals = args.Qualifiers.RoundOf8 |> List.map TeamId.create
                SemiFinals = args.Qualifiers.RoundOf4 |> List.map TeamId.create
                Finals = args.Qualifiers.RoundOf2 |> List.map TeamId.create
                Winner = TeamId.create args.Winner
                TopScorers = args.TopScorers |> List.map PlayerId.create
            }
        let predictionResult = PredictionResult.create predictionId args.Name predictions version
        InMemoryStore.predictionResults.Add(predictionResult.PredictionId, predictionResult)
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
    InMemoryStore.predictionResults.Values
        |> Seq.toList

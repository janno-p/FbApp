[<RequireQualifiedAccess>]
module FbApp.Domain.Fixtures

open System

let [<Literal>] AggregateName = "Fixture"

type Error =
    | FixtureAlreadyAdded
    | UnknownFixture

type FixtureStatus =
    | Scheduled
    | Timed
    | InPlay
    | Finished
    | Postponed
    | Canceled
    | Unknown of string
with
    static member FromString (value) =
        match value with
        | "SCHEDULED" -> Scheduled
        | "TIMED" -> Timed
        | "IN_PLAY" -> InPlay
        | "FINISHED" -> Finished
        | "POSTPONED" -> Postponed
        | "CANCELED" -> Canceled
        | status -> Unknown status
    override this.ToString() =
        match this with
        | Scheduled -> "SCHEDULED"
        | Timed -> "TIMED"
        | InPlay -> "IN_PLAY"
        | Finished -> "FINISHED"
        | Postponed -> "POSTPONED"
        | Canceled -> "CANCELED"
        | Unknown x -> x

type State =
    {
        Date: DateTimeOffset
        Status: FixtureStatus
        Score: (int * int) option
    }

type AddFixtureInput =
    {
        CompetitionId: Guid
        ExternalId: int64
        HomeTeamId: int64
        AwayTeamId: int64
        Date: DateTimeOffset
        Status: string
        Matchday: int
    }

type UpdateFixtureInput =
    {
        Status: string
        Result: (int * int) option
    }

type UpdateQualifiersInput =
    {
        CompetitionId: Guid
        ExternalId: int64
        HomeTeamId: int64
        AwayTeamId: int64
        Date: DateTimeOffset
        Matchday: int
        Status: string
        Result: (int * int) option
    }

type Event =
    | Added of AddFixtureInput
    | StatusChanged of FixtureStatus
    | ScoreChanged of int * int

type Command =
    | AddFixture of AddFixtureInput
    | UpdateFixture of UpdateFixtureInput
    | UpdateQualifiers of UpdateQualifiersInput

let decide : State option -> Command -> Result<Event list, Error> =
    (fun state -> function
        | AddFixture input ->
            match state with
            | Some(_) -> Error(FixtureAlreadyAdded)
            | None -> Ok([Added input])
        | UpdateFixture input ->
            match state with
            | Some(state) ->
                Ok([
                    let status = FixtureStatus.FromString(input.Status)
                    if status <> state.Status then
                        yield StatusChanged status

                    match input.Result with
                    | Some(score) when input.Result <> state.Score ->
                        yield ScoreChanged score
                    | _ -> ()
                ])
            | None -> Error(UnknownFixture)
        | UpdateQualifiers input ->
            match state with
            | Some(state) ->
                Ok([
                    let status = FixtureStatus.FromString(input.Status)
                    if status <> state.Status then
                        yield StatusChanged status

                    match input.Result with
                    | Some(score) when input.Result <> state.Score ->
                        yield ScoreChanged score
                    | _ -> ()
                ])
            | None ->
                Ok([
                    yield Added { CompetitionId = input.CompetitionId; ExternalId = input.ExternalId; HomeTeamId = input.HomeTeamId; AwayTeamId = input.AwayTeamId; Date = input.Date; Status = input.Status; Matchday = input.Matchday }
                    if input.Result.IsSome then yield ScoreChanged input.Result.Value
                ])
    )

let evolve : State option -> Event -> State =
    (fun state -> function
        | Added input ->
            { Date = input.Date; Status = FixtureStatus.FromString(input.Status); Score = None }
        | StatusChanged status ->
            let score =
                match status, state.Value.Score with
                | InPlay, None -> Some(0, 0)
                | _, score -> score
            { state.Value with Status = status; Score = score }
        | ScoreChanged (homeGoals, awayGoals) ->
            { state.Value with Score = Some(homeGoals, awayGoals) }
    )

let fixturesNamespace =
    Guid.Parse("2130666a-7b4b-44c7-9d0a-da020138ffc0")

let createId (competitionId: Guid, externalId: int64) =
    Guid.createDeterministicGuid fixturesNamespace (sprintf "%s-%s" (competitionId.ToString("N")) (externalId.ToString()))

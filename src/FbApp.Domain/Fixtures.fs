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
        Penalties: (int * int) option
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
        Penalties: (int * int) option
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
        Penalties: (int * int) option
    }

type ScoreChangedArgs =
    {
        Goals: int * int
        Penalties: (int * int) option
    }

type Event =
    | Added of AddFixtureInput
    | StatusChanged of FixtureStatus
    | ScoreChanged of int * int
    | ScoreChanged2 of ScoreChangedArgs

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

                    match input.Result, input.Penalties with
                    | Some(score), penalties when input.Result <> state.Score || penalties <> state.Penalties ->
                        yield ScoreChanged2 { Goals = score; Penalties = input.Penalties }
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

                    match input.Result, input.Penalties with
                    | Some(score), penalties when input.Result <> state.Score || penalties <> state.Penalties ->
                        yield ScoreChanged2 { Goals = score; Penalties = penalties }
                    | _ -> ()
                ])
            | None ->
                Ok([
                    yield Added { CompetitionId = input.CompetitionId; ExternalId = input.ExternalId; HomeTeamId = input.HomeTeamId; AwayTeamId = input.AwayTeamId; Date = input.Date; Status = input.Status; Matchday = input.Matchday }
                    if input.Result.IsSome then yield ScoreChanged2 { Goals = input.Result.Value; Penalties = input.Penalties }
                ])
    )

let evolve : State option -> Event -> State =
    (fun state -> function
        | Added input ->
            { Date = input.Date; Status = FixtureStatus.FromString(input.Status); Score = None; Penalties = None }
        | StatusChanged status ->
            let score =
                match status, state.Value.Score with
                | InPlay, None -> Some(0, 0)
                | _, score -> score
            { state.Value with Status = status; Score = score }
        | ScoreChanged (homeGoals, awayGoals) ->
            { state.Value with Score = Some(homeGoals, awayGoals) }
        | ScoreChanged2 { Goals = (homeGoals, awayGoals); Penalties = penalties } ->
            { state.Value with Score = Some(homeGoals, awayGoals); Penalties = penalties }
    )

let fixturesNamespace =
    Guid.Parse("2130666a-7b4b-44c7-9d0a-da020138ffc0")

let createId (competitionId: Guid, externalId: int64) =
    Guid.createDeterministicGuid fixturesNamespace (sprintf "%s-%s" (competitionId.ToString("N")) (externalId.ToString()))

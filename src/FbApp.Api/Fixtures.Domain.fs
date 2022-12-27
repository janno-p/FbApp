[<RequireQualifiedAccess>]
module FbApp.Api.Domain.Fixtures

open Be.Vlaanderen.Basisregisters.Generators.Guid
open System

let [<Literal>] AggregateName = "Fixture"

type Error =
    | FixtureAlreadyAdded
    | UnknownFixture

type FixtureStage =
    | Group
    | Last16
    | QuarterFinals
    | SemiFinals
    | ThirdPlace
    | Final

module FixtureStage =
    let tryFromString = function
        | "GROUP_STAGE" -> Some Group
        | "LAST_16" -> Some Last16
        | "QUARTER_FINALS" -> Some QuarterFinals
        | "SEMI_FINALS" -> Some SemiFinals
        | "THIRD_PLACE" -> Some ThirdPlace
        | "FINAL" -> Some Final
        | _ -> None

type FixtureStatus =
    | Scheduled
    | Timed
    | InPlay
    | Paused
    | Finished
    | Suspended
    | Postponed
    | Cancelled
    | Awarded
    | Unknown of string
with
    static member FromString value =
        match value with
        | "SCHEDULED" -> Scheduled
        | "TIMED" -> Timed
        | "IN_PLAY" -> InPlay
        | "PAUSED" -> Paused
        | "FINISHED" -> Finished
        | "SUSPENDED" -> Suspended
        | "POSTPONED" -> Postponed
        | "CANCELLED" -> Cancelled
        | "AWARDED" -> Awarded
        | status -> Unknown status
    override this.ToString() =
        match this with
        | Scheduled -> "SCHEDULED"
        | Timed -> "TIMED"
        | InPlay -> "IN_PLAY"
        | Paused -> "PAUSED"
        | Finished -> "FINISHED"
        | Suspended -> "SUSPENDED"
        | Postponed -> "POSTPONED"
        | Cancelled -> "CANCELLED"
        | Awarded -> "AWARDED"
        | Unknown x -> x

type FixtureGoals =
    {
        Home: int
        Away: int
    }

type State =
    {
        Date: DateTimeOffset
        Status: FixtureStatus
        FullTime: FixtureGoals option
        ExtraTime: FixtureGoals option
        Penalties: FixtureGoals option
    }

type AddFixtureInput =
    {
        CompetitionId: Guid
        ExternalId: int64
        HomeTeamId: int64
        AwayTeamId: int64
        Date: DateTimeOffset
        Status: string
        Stage: string
    }

type UpdateFixtureInput =
    {
        Status: string
        FullTime: FixtureGoals option
        ExtraTime: FixtureGoals option
        Penalties: FixtureGoals option
    }

type UpdateQualifiersInput =
    {
        CompetitionId: Guid
        ExternalId: int64
        HomeTeamId: int64
        AwayTeamId: int64
        Date: DateTimeOffset
        Stage: string
        Status: string
        FullTime: FixtureGoals option
        ExtraTime: FixtureGoals option
        Penalties: FixtureGoals option
    }

type ScoreChangedArgs =
    {
        FullTime: FixtureGoals
        ExtraTime: FixtureGoals option
        Penalties: FixtureGoals option
    }

type Event =
    | Added of AddFixtureInput
    | StatusChanged of FixtureStatus
    | ScoreChanged2 of ScoreChangedArgs

type Command =
    | AddFixture of AddFixtureInput
    | UpdateFixture of UpdateFixtureInput
    | UpdateQualifiers of UpdateQualifiersInput

let decide : State option -> Command -> Result<Event list, Error> =
    (fun state -> function
        | AddFixture input ->
            match state with
            | Some _ -> Error(FixtureAlreadyAdded)
            | None -> Ok([Added input])
        | UpdateFixture input ->
            match state with
            | Some(state) ->
                Ok([
                    let status = FixtureStatus.FromString(input.Status)
                    if status <> state.Status then
                        yield StatusChanged status

                    match input.FullTime, input.ExtraTime, input.Penalties with
                    | Some(fullTime), extraTime, penalties when input.FullTime <> state.FullTime || extraTime <> state.ExtraTime || penalties <> state.Penalties ->
                        yield ScoreChanged2 { FullTime = fullTime; ExtraTime = extraTime; Penalties = penalties }
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

                    match input.FullTime, input.ExtraTime, input.Penalties with
                    | Some(fullTime), extraTime, penalties when input.FullTime <> state.FullTime || extraTime <> state.ExtraTime || penalties <> state.Penalties ->
                        yield ScoreChanged2 { FullTime = fullTime; ExtraTime = extraTime; Penalties = penalties }
                    | _ -> ()
                ])
            | None ->
                Ok([
                    yield Added { CompetitionId = input.CompetitionId; ExternalId = input.ExternalId; HomeTeamId = input.HomeTeamId; AwayTeamId = input.AwayTeamId; Date = input.Date; Status = input.Status; Stage = input.Stage }
                    if input.FullTime.IsSome then yield ScoreChanged2 { FullTime = input.FullTime.Value; ExtraTime = input.ExtraTime; Penalties = input.Penalties }
                ])
    )

let evolve : State option -> Event -> State =
    (fun state -> function
        | Added input ->
            { Date = input.Date; Status = FixtureStatus.FromString(input.Status); FullTime = None; ExtraTime = None; Penalties = None }
        | StatusChanged status ->
            let fullTime =
                match status, state.Value.FullTime with
                | InPlay, None -> Some({ Home = 0; Away = 0 })
                | _, fullTime -> fullTime
            { state.Value with Status = status; FullTime = fullTime }
        | ScoreChanged2 { FullTime = fullTime; ExtraTime = extraTime; Penalties = penalties } ->
            { state.Value with FullTime = Some(fullTime); ExtraTime = extraTime; Penalties = penalties }
    )

let fixturesNamespace =
    Guid "2130666a-7b4b-44c7-9d0a-da020138ffc0"

let createId (competitionId: Guid, externalId: int64) =
    Deterministic.Create(fixturesNamespace, sprintf "%s-%s" (competitionId.ToString("N")) (externalId.ToString()), 5)

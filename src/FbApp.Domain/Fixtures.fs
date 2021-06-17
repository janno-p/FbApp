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
        | ScoreChanged (homeGoals, awayGoals) ->
            { state.Value with FullTime = Some({ Home = homeGoals; Away = awayGoals }) }
        | ScoreChanged2 { FullTime = fullTime; ExtraTime = extraTime; Penalties = penalties } ->
            { state.Value with FullTime = Some(fullTime); ExtraTime = extraTime; Penalties = penalties }
    )

let fixturesNamespace =
    Guid.Parse("2130666a-7b4b-44c7-9d0a-da020138ffc0")

let createId (competitionId: Guid, externalId: int64) =
    Guid.createDeterministicGuid fixturesNamespace (sprintf "%s-%s" (competitionId.ToString("N")) (externalId.ToString()))

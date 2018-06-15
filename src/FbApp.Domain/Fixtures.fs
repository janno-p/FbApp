[<RequireQualifiedAccess>]
module FbApp.Domain.Fixtures

open System

let [<Literal>] AggregateName = "Fixture"

type Id = Guid * int64

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

type FixtureState =
    {
        Date: DateTime
        Status: FixtureStatus
        Score: (int * int) option
    }

type State = FixtureState option

let initialState : State = None

type AddFixtureInput =
    {
        CompetitionId: Guid
        ExternalId: int64
        HomeTeamId: int64
        AwayTeamId: int64
        Date: DateTime
        Status: string
    }

type UpdateFixtureInput =
    {
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

let decide : State -> Command -> Result<Event list, Error> =
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
    )

let evolve : State -> Event -> State =
    (fun state -> function
        | Added input -> Some({ Date = input.Date; Status = FixtureStatus.FromString(input.Status); Score = None })
        | StatusChanged status -> Some({ state.Value with Status = status })
        | ScoreChanged (homeGoals, awayGoals) -> Some({ state.Value with Score = Some(homeGoals, awayGoals) })
    )

let fixturesNamespace =
    Guid.Parse("2130666a-7b4b-44c7-9d0a-da020138ffc0")

let streamId (id: Id) =
    let guid, externalId = id
    Guid.createDeterministicGuid fixturesNamespace (sprintf "%s-%s" (guid.ToString("N")) (externalId.ToString()))

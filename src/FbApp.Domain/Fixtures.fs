[<RequireQualifiedAccess>]
module FbApp.Domain.Fixtures

open System

let [<Literal>] AggregateName = "Fixture"

type Id = Guid * int64

type FixtureStatus =
    | Scheduled
    | InPlay
    | Finished

type State =
    {
        Date: DateTime
        Status: FixtureStatus
        Score: (int * int) option
    }

let initialState : State =
    {
        Date = Unchecked.defaultof<DateTime>
        Status = FixtureStatus.Scheduled
        Score = None
    }

type FixtureScheduledInput =
    {
        CompetitionId: Guid
        ExternalId: int64
        HomeTeamId: int64
        AwayTeamId: int64
        Date: DateTime
    }

type UpdateFixtureInput =
    {
        Status: string
        Result: (int * int) option
    }

type Event =
    | Scheduled of FixtureScheduledInput
    | Started
    | ScoreChanged of int * int
    | Completed

type Command =
    | ScheduleFixture of FixtureScheduledInput
    | UpdateFixture of UpdateFixtureInput

let decide : State -> Command -> Result<Event list, unit> =
    (fun state -> function
        | ScheduleFixture input ->
            Ok([Scheduled input])
        | UpdateFixture input ->
            Ok([
                match input.Status with
                | "IN_PLAY" when state.Status <> InPlay ->
                    yield Started
                | "FINISHED" when state.Status <> Finished ->
                    yield Completed
                | _ -> ()

                match input.Result with
                | Some(score) when input.Result <> state.Score ->
                    yield ScoreChanged score
                | _ -> ()
            ])
    )

let evolve : State -> Event -> State =
    (fun state -> function
        | Scheduled input -> { state with Date = input.Date; Status = FixtureStatus.Scheduled }
        | Started -> { state with Status = InPlay; Score = Some(0, 0) }
        | ScoreChanged (homeGoals, awayGoals) -> { state with Score = Some(homeGoals, awayGoals) }
        | Completed -> { state with Status = Finished }
    )

let fixturesNamespace =
    Guid.Parse("2130666a-7b4b-44c7-9d0a-da020138ffc0")

let streamId (id: Id) =
    let guid, externalId = id
    Guid.createDeterministicGuid fixturesNamespace (sprintf "%s-%s" (guid.ToString("N")) (externalId.ToString()))

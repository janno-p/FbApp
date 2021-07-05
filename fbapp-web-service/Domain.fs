namespace FbApp.Domain

open System
open System.Text
open XploRe.Util

[<RequireQualifiedAccess>]
module Uuid =
    let createDeterministicUuid (namespaceId: Uuid) (name: string) =
        if name |> isNull then failwith "Argument null exception 'name'"
        let nameBytes = Encoding.UTF8.GetBytes(name)
        Uuid.NewNameBasedV5(namespaceId, nameBytes)

[<RequireQualifiedAccess>]
module Competitions =
    let [<Literal>] AggregateName = "Competition"

    type State = unit

    type Created =
        {
            Description: string
            ExternalId: int64
            Date: DateTimeOffset
        }

    type TeamAssignment =
        {
            Name: string
            Code: string
            FlagUrl: string
            ExternalId: int64
        }

    type FixtureAssignment =
        {
            HomeTeamId: int64
            AwayTeamId: int64
            Date: DateTimeOffset
            ExternalId: int64
        }

    type GroupAssignment = string * int64[]

    type CreateInput =
        {
            Description: string
            ExternalId: int64
            Date: DateTimeOffset
        }

    type Command =
        | Create of CreateInput
        | AssignTeamsAndFixtures of TeamAssignment list * FixtureAssignment list * GroupAssignment list

    type Event =
        | Created of Created
        | TeamsAssigned of TeamAssignment list
        | FixturesAssigned of FixtureAssignment list
        | GroupsAssigned of GroupAssignment list

    let decide: State option -> Command -> Result<Event list,unit> =
        (fun _ -> function
            | Create { Description = description; ExternalId = externalId; Date = date } ->
                Ok([Created { Description = description; ExternalId = externalId; Date = date }])
            | AssignTeamsAndFixtures (teams, fixtures, groups) ->
                Ok([TeamsAssigned teams; FixturesAssigned fixtures; GroupsAssigned groups])
        )

    let evolve: State option -> Event -> State =
        (fun _ -> function
            | Created _ -> ()
            | TeamsAssigned _ -> ()
            | FixturesAssigned _ -> ()
            | GroupsAssigned _ -> ()
        )

    let competitionsNamespace =
        Uuid "1dc53967-8c3b-49a9-9496-27a2267bbef7"

    let createId (externalId: int64) =
        Uuid.createDeterministicUuid competitionsNamespace (externalId.ToString())

[<RequireQualifiedAccess>]
module Fixtures =
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
        static member FromString value =
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
            CompetitionId: Uuid
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
            CompetitionId: Uuid
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
            | ScoreChanged (homeGoals, awayGoals) ->
                { state.Value with FullTime = Some({ Home = homeGoals; Away = awayGoals }) }
            | ScoreChanged2 { FullTime = fullTime; ExtraTime = extraTime; Penalties = penalties } ->
                { state.Value with FullTime = Some(fullTime); ExtraTime = extraTime; Penalties = penalties }
        )

    let fixturesNamespace =
        Uuid "2130666a-7b4b-44c7-9d0a-da020138ffc0"

    let createId (competitionId: Uuid, externalId: int64) =
        Uuid.createDeterministicUuid fixturesNamespace (sprintf "%s-%s" (competitionId.ToString("N")) (externalId.ToString()))

[<RequireQualifiedAccess>]
module Leagues =
    let [<Literal>] AggregateName = "League"

    type State = unit

    type CreateLeagueInput =
        {
            CompetitionId: Uuid
            Code: string
            Name: string
        }

    type Command =
        | Create of CreateLeagueInput
        | AddPrediction of Uuid

    type Event =
        | Created of CreateLeagueInput
        | PredictionAdded of Uuid

    let decide : State option -> Command -> Result<Event list, unit> =
        (fun _ -> function
            | Create input ->
                Ok([Created input])
            | AddPrediction id ->
                Ok([PredictionAdded id])
        )

    let evolve : State option -> Event -> State =
        (fun _ -> function
            | Created _ -> ()
            | PredictionAdded _ -> ()
        )

    let leaguesNamespace =
        Uuid "866b5bbe-3053-4717-ad46-30966dc9fe32"

    let createId (competitionId: Uuid, leagueCode: string) =
        Uuid.createDeterministicUuid leaguesNamespace (sprintf "%s-%s" (competitionId.ToString("N")) leagueCode)

[<RequireQualifiedAccess>]
module Predictions =
    let [<Literal>] AggregateName = "Prediction"

    type Email = Email of string

    type State =
        {
            IsAccepted: bool
        }

    [<CLIMutable>]
    type FixtureResultRegistrationInput =
        {
            Id: int64
            Result: string
        }

    [<CLIMutable>]
    type QualifiersRegistrationInput =
        {
            RoundOf16: int64 array
            RoundOf8: int64 array
            RoundOf4: int64 array
            RoundOf2: int64 array
        }

    [<CLIMutable>]
    type PredictionRegistrationInput =
        {
            CompetitionId: Uuid
            Fixtures: FixtureResultRegistrationInput[]
            Qualifiers: QualifiersRegistrationInput
            Winner: int64
        }

    type FixtureResult =
        | HomeWin
        | Tie
        | AwayWin

    type FixtureResultRegistration =
        {
            Id: int64
            Result: FixtureResult
        }

    type QualifiersRegistration =
        {
            RoundOf16: int64 list
            RoundOf8: int64 list
            RoundOf4: int64 list
            RoundOf2: int64 list
        }

    type PredictionRegistration =
        {
            Name: string
            Email: string
            CompetitionId: Uuid
            Fixtures: FixtureResultRegistration list
            Qualifiers: QualifiersRegistration
            Winner: int64
        }

    type Command =
        | Register of PredictionRegistrationInput * string * string
        | Decline

    type Event =
        | Registered of PredictionRegistration
        | Declined

    let decide: State option -> Command -> Result<Event list,unit> =
        (fun _ -> function
            | Register (input, name, email) ->
                let mapResult = function
                    | "HOME" -> HomeWin
                    | "TIE" -> Tie
                    | "AWAY" -> AwayWin
                    | other -> failwith $"Invalid result value: %s{other}"
                let registration =
                    {
                        Name = name
                        Email = email
                        CompetitionId = input.CompetitionId
                        Fixtures =
                            input.Fixtures
                            |> Seq.map (fun x ->
                                {
                                    Id = x.Id
                                    Result = mapResult x.Result
                                })
                            |> Seq.toList
                        Qualifiers =
                            {
                                RoundOf16 =
                                    input.Qualifiers.RoundOf16
                                    |> List.ofArray
                                RoundOf8 =
                                    input.Qualifiers.RoundOf8
                                    |> List.ofArray
                                RoundOf4 =
                                    input.Qualifiers.RoundOf4
                                    |> List.ofArray
                                RoundOf2 =
                                    input.Qualifiers.RoundOf2
                                    |> List.ofArray
                            }
                        Winner = input.Winner
                    }
                Ok([Registered registration])
            | Decline -> Ok([Declined])
        )

    let evolve: State option -> Event -> State =
        (fun state -> function
            | Registered _ -> { IsAccepted = true }
            | Declined -> { state.Value with IsAccepted = false }
        )

    let predictionsNamespace =
        Uuid "2945d861-0b2f-4783-914b-97988b98c76b"

    let createId (competitionId: Uuid, Email email) =
        Uuid.createDeterministicUuid predictionsNamespace (sprintf "%s-%s" (competitionId.ToString("N")) email)

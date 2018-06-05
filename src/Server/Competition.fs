[<RequireQualifiedAccess>]
module FbApp.Server.Competition

open System

type State = unit

let initialState =
    ()

type Created =
    {
        Description: string
        ExternalSource: int64
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
        Date: DateTime
        ExternalId: int64
    }

type Command =
    | Create of string * int64
    | AssignTeams of TeamAssignment list
    | AssignFixtures of FixtureAssignment list

type Event =
    | Created of Created
    | TeamsAssigned of TeamAssignment list
    | FixturesAssigned of FixtureAssignment list

let decide: State -> Command -> Result<Event list,unit> =
    (fun state -> function
        | Create (description, externalSource) ->
            Ok([Created { Description = description; ExternalSource = externalSource }])
        | AssignTeams teams ->
            Ok([TeamsAssigned teams])
        | AssignFixtures fixtures ->
            Ok([FixturesAssigned fixtures])
    )

let evolve: State -> Event -> State =
    (fun state -> function
        | Created args -> ()
        | TeamsAssigned _ -> ()
        | FixturesAssigned _ -> ()
    )

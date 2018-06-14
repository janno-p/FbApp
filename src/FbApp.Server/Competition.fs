[<RequireQualifiedAccess>]
module FbApp.Server.Competition

open System
open System.Collections.Generic

type Id = Guid

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

type GroupAssignment = string * int64[]

type Command =
    | Create of string * int64
    | AssignTeamsAndFixtures of TeamAssignment list * FixtureAssignment list * GroupAssignment list

type Event =
    | Created of Created
    | TeamsAssigned of TeamAssignment list
    | FixturesAssigned of FixtureAssignment list
    | GroupsAssigned of GroupAssignment list

let decide: State -> Command -> Result<Event list,unit> =
    (fun _ -> function
        | Create (description, externalSource) ->
            Ok([Created { Description = description; ExternalSource = externalSource }])
        | AssignTeamsAndFixtures (teams, fixtures, groups) ->
            Ok([TeamsAssigned teams; FixturesAssigned fixtures; GroupsAssigned groups])
    )

let evolve: State -> Event -> State =
    (fun _ -> function
        | Created _ -> ()
        | TeamsAssigned _ -> ()
        | FixturesAssigned _ -> ()
        | GroupsAssigned _ -> ()
    )

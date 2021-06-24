[<RequireQualifiedAccess>]
module FbApp.Api.Domain.Competitions

open System

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
    Guid.Parse("1dc53967-8c3b-49a9-9496-27a2267bbef7")

let createId (externalId: int64) =
    // Uuid.NewNameBasedV5(competitionsNamespace.ToUuid(), externalId.ToString()).ToGuid()
    Guid.createDeterministicGuid competitionsNamespace (externalId.ToString())

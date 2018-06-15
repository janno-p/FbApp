[<RequireQualifiedAccess>]
module FbApp.Domain.Competitions

open System

let [<Literal>] AggregateName = "Competition"

type Id = int64

type State = unit

let initialState =
    ()

type Created =
    {
        Description: string
        ExternalId: int64
        Date: DateTime
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

type CreateInput =
    {
        Description: string
        ExternalId: int64
        Date: DateTime
    }

type Command =
    | Create of CreateInput
    | AssignTeamsAndFixtures of TeamAssignment list * FixtureAssignment list * GroupAssignment list

type Event =
    | Created of Created
    | TeamsAssigned of TeamAssignment list
    | FixturesAssigned of FixtureAssignment list
    | GroupsAssigned of GroupAssignment list

let decide: State -> Command -> Result<Event list,unit> =
    (fun _ -> function
        | Create { Description = description; ExternalId = externalId; Date = date } ->
            Ok([Created { Description = description; ExternalId = externalId; Date = date }])
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

let competitionsNamespace =
    Guid.Parse("1dc53967-8c3b-49a9-9496-27a2267bbef7")

let streamId (externalId: Id) =
    Guid.createDeterministicGuid competitionsNamespace (externalId.ToString())

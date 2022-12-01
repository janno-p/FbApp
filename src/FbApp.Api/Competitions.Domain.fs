[<RequireQualifiedAccess>]
module FbApp.Api.Domain.Competitions

open System
open Be.Vlaanderen.Basisregisters.Generators.Guid

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

type PlayerAssignment =
    {
        Name: string
        Position: string
        TeamExternalId: int64
        ExternalId: int64
    }

type GroupAssignment = string * int64[]

type CreateInput =
    {
        Description: string
        ExternalId: int64
        Date: DateTimeOffset
    }

type StandingRow =
    {
        Position: int
        TeamId: int64
        PlayedGames: int
        Won: int
        Draw: int
        Lost: int
        GoalsFor: int
        GoalsAgainst: int
    }

type Command =
    | Create of CreateInput
    | AssignTeamsAndFixtures of TeamAssignment list * FixtureAssignment list * GroupAssignment list * PlayerAssignment list
    | UpdateStandings of string * StandingRow list

type Event =
    | Created of Created
    | TeamsAssigned of TeamAssignment list
    | FixturesAssigned of FixtureAssignment list
    | GroupsAssigned of GroupAssignment list
    | PlayersAssigned of PlayerAssignment list
    | StandingsUpdated of string * StandingRow list

let decide: State option -> Command -> Result<Event list,unit> =
    (fun _ -> function
        | Create { Description = description; ExternalId = externalId; Date = date } ->
            Ok([Created { Description = description; ExternalId = externalId; Date = date }])
        | AssignTeamsAndFixtures (teams, fixtures, groups, players) ->
            Ok([TeamsAssigned teams; FixturesAssigned fixtures; GroupsAssigned groups; PlayersAssigned players])
        | UpdateStandings(s, standingRowInputs) ->
            Ok([StandingsUpdated (s, standingRowInputs)])
    )

let evolve: State option -> Event -> State =
    (fun _ -> function
        | Created _ -> ()
        | TeamsAssigned _ -> ()
        | FixturesAssigned _ -> ()
        | GroupsAssigned _ -> ()
        | PlayersAssigned _ -> ()
        | StandingsUpdated _ -> ()
    )

let competitionsNamespace =
    Guid "1dc53967-8c3b-49a9-9496-27a2267bbef7"

let createId (externalId: int64) =
    Deterministic.Create(competitionsNamespace, externalId.ToString(), 5)

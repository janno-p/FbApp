module FbApp.Api.Domain.Leagues

open System
open Be.Vlaanderen.Basisregisters.Generators.Guid

let [<Literal>] AggregateName = "League"

type State = unit

type CreateLeagueInput =
    {
        CompetitionId: Guid
        Code: string
        Name: string
    }

type Command =
    | Create of CreateLeagueInput
    | AddPrediction of Guid

type Event =
    | Created of CreateLeagueInput
    | PredictionAdded of Guid

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

let leaguesGuid = Guid "866b5bbe-3053-4717-ad46-30966dc9fe32"

let createId (competitionId: Guid, leagueCode: string) =
    Deterministic.Create(leaguesGuid, (sprintf "%s-%s" (competitionId.ToString("N")) leagueCode), 5)

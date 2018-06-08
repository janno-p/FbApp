[<RequireQualifiedAccess>]
module FbApp.Server.Aggregate

open Giraffe
open System
open System.Threading.Tasks

type Aggregate<'Id, 'State, 'Command, 'Event, 'Error> =
    {
        InitialState: 'State
        Decide: 'State -> 'Command -> Result<'Event list, 'Error>
        Evolve: 'State -> 'Event -> 'State
        StreamId: 'Id -> Guid
    }

type CommandHandler<'Id, 'Command, 'Error> = 'Id * int64 option -> 'Command -> Task<Result<int64, 'Error>>

let makeHandler (aggregate: Aggregate<'Id, 'State, 'Command, 'Event, 'Error>)
                (load: Type * Guid -> Task<(int64 * obj seq)>, commit: Guid * int64 -> 'Event list -> Task<int64>) : CommandHandler<'Id, 'Command, 'Error> =
    fun (id, version) command -> task {
        let streamId = aggregate.StreamId id
        let! ver, events = load (typeof<'Event>, streamId)
        let events = events |> Seq.cast<'Event>
        let state = events |> Seq.fold aggregate.Evolve aggregate.InitialState
        match aggregate.Decide state command with
        | Ok(events) ->
            let! result = commit (streamId, version |> Option.defaultValue ver) events
            return Ok(result)
        | Error(err) ->
            return Error(err)
    }

module Handlers =
    type CompetitionHandler = CommandHandler<Competition.Id, Competition.Command, unit>
    let mutable competitionHandler = Unchecked.defaultof<CompetitionHandler>

    type PredictionHandler = CommandHandler<Prediction.Id, Prediction.Command, unit>
    let mutable predictionHandler = Unchecked.defaultof<PredictionHandler>

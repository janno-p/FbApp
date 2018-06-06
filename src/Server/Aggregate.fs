[<RequireQualifiedAccess>]
module FbApp.Server.Aggregate

open Giraffe
open System
open System.Threading.Tasks

type Aggregate<'State, 'Command, 'Event, 'Error> =
    {
        InitialState: 'State
        Decide: 'State -> 'Command -> Result<'Event list, 'Error>
        Evolve: 'State -> 'Event -> 'State
    }

type Id = Guid
type CommandHandler<'Command, 'Error> = Id * int64 -> 'Command -> Task<Result<int64, 'Error>>

let makeHandler (aggregate: Aggregate<'State, 'Command, 'Event, 'Error>)
                (load: Type * Id -> Task<obj seq>, commit: Id * int64 -> 'Event list -> Task<int64>) : CommandHandler<'Command, 'Error> =
    fun (id, version) command -> task {
        let! events = load (typeof<'Event>, id)
        let events = events |> Seq.cast<'Event>
        let state = events |> Seq.fold aggregate.Evolve aggregate.InitialState
        match aggregate.Decide state command with
        | Ok(events) ->
            let! result = commit (id, version) events
            return Ok(result)
        | Error(err) ->
            return Error(err)
    }

module Handlers =
    type CompetitionHandler = CommandHandler<Competition.Command, unit>
    let mutable competitionHandler = Unchecked.defaultof<CompetitionHandler>

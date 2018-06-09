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

type AggregateError<'Error> =
    | DomainError of 'Error
    | WrongExpectedVersion
    | Other of exn

type TaskResult<'T, 'E> = Task<Result<'T, 'E>>

type CommandHandler<'Id, 'Command, 'Error> = 'Id * int64 option -> 'Command -> TaskResult<int64, AggregateError<'Error>>
type LoadAggregateEvents<'Event> = Type * Guid -> Task<(int64 * 'Event seq)>
type CommitAggregateEvents<'Event, 'Error> = Guid * int64 -> 'Event list -> TaskResult<int64, AggregateError<'Error>>

let makeHandler (aggregate: Aggregate<'Id, 'State, 'Command, 'Event, 'Error>)
                (load: LoadAggregateEvents<'Event>, commit: CommitAggregateEvents<'Event, 'Error>) : CommandHandler<'Id, 'Command, 'Error> =
    fun (id, version) command -> task {
        let streamId = aggregate.StreamId id
        let! ver, events = load (typeof<'Event>, streamId)
        let state = events |> Seq.fold aggregate.Evolve aggregate.InitialState
        match aggregate.Decide state command with
        | Ok(events) ->
            return! commit (streamId, version |> Option.defaultValue ver) events
        | Error(err) ->
            return Error(DomainError(err))
    }

module Handlers =
    type CompetitionHandler = CommandHandler<Competition.Id, Competition.Command, unit>
    let mutable competitionHandler = Unchecked.defaultof<CompetitionHandler>

    type PredictionHandler = CommandHandler<Prediction.Id, Prediction.Command, unit>
    let mutable predictionHandler = Unchecked.defaultof<PredictionHandler>

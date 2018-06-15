module FbApp.Core.Aggregate

open FSharp.Control.Tasks.ContextInsensitive
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

type ExpectedVersion =
    | New
    | Version of int64
    | Any

type ExpectedCommitVersion =
    | NewStream
    | Value of int64

type TaskResult<'T, 'E> = Task<Result<'T, 'E>>

type CommandHandler<'Id, 'Command, 'Error> = 'Id * ExpectedVersion -> 'Command -> TaskResult<int64, AggregateError<'Error>>
type LoadAggregateEvents<'Event> = Type * Guid -> Task<(int64 * 'Event seq)>
type CommitAggregateEvents<'Event, 'Error> = Guid * ExpectedCommitVersion -> 'Event list -> TaskResult<int64, AggregateError<'Error>>

let makeHandler (aggregate: Aggregate<'Id, 'State, 'Command, 'Event, 'Error>)
                (load: LoadAggregateEvents<'Event>, commit: CommitAggregateEvents<'Event, 'Error>) : CommandHandler<'Id, 'Command, 'Error> =
    fun (id, expectedVersion) command -> task {
        let streamId = aggregate.StreamId id
        let! ver, events = load (typeof<'Event>, streamId)
        let state = events |> Seq.fold aggregate.Evolve aggregate.InitialState
        match aggregate.Decide state command with
        | Ok(events) ->
            let expectedCommitVersion =
                match expectedVersion with
                | ExpectedVersion.New -> ExpectedCommitVersion.NewStream
                | ExpectedVersion.Version v -> ExpectedCommitVersion.Value v
                | ExpectedVersion.Any -> ExpectedCommitVersion.Value ver
            return! commit (streamId, expectedCommitVersion) events
        | Error(err) ->
            return Error(DomainError(err))
    }

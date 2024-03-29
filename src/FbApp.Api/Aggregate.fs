module FbApp.Api.Aggregate

open System
open System.Threading.Tasks

type Aggregate<'State, 'Command, 'Event, 'Error> =
    {
        Decide: 'State option -> 'Command -> Result<'Event list, 'Error>
        Evolve: 'State option -> 'Event -> 'State
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

type CommandHandler<'Command, 'Error> = Guid * ExpectedVersion -> 'Command -> TaskResult<int64, AggregateError<'Error>>
type LoadAggregateEvents<'Event> = Type * Guid -> Task<int64 option * 'Event seq>
type CommitAggregateEvents<'Event, 'Error> = Guid * ExpectedCommitVersion -> 'Event list -> TaskResult<int64, AggregateError<'Error>>

let makeHandler (aggregate: Aggregate<'State, 'Command, 'Event, 'Error>)
                (load: LoadAggregateEvents<'Event>, commit: CommitAggregateEvents<'Event, 'Error>) : CommandHandler<'Command, 'Error> =
    fun (streamId, expectedVersion) command -> task {
        let! streamPosition, events = load (typeof<'Event>, streamId)
        let state = events |> Seq.fold (fun state event -> Some(aggregate.Evolve state event)) Option<'State>.None
        match aggregate.Decide state command with
        | Ok(events) ->
            let expectedCommitVersion =
                match (expectedVersion, streamPosition) with
                | New, _ -> NewStream
                | Version v, _ -> Value v
                | Any, Some ver -> Value ver
                | Any, None -> NewStream
            return! commit (streamId, expectedCommitVersion) events
        | Error(err) ->
            return Error(DomainError(err))
    }

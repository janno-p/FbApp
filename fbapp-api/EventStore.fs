module FbApp.Api.EventStore

open EventStore.ClientAPI
open EventStore.ClientAPI.Exceptions
open EventStore.ClientAPI.SystemData
open FbApp.Api.Aggregate
open FSharp.Control.Tasks
open System

let [<Literal>] ApplicationName = "FbApp"

let epoch = DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)

let toUnixTime (dateTimeOffset: DateTimeOffset) =
    Convert.ToInt64((dateTimeOffset.UtcDateTime - epoch).TotalSeconds);

[<CLIMutable>]
type EventStoreOptions =
    {
        Uri: string
        UserName: string
        Password: string
    }
with
    member this.UserCredentials =
        UserCredentials(this.UserName, this.Password)

let createEventStoreConnection (options: EventStoreOptions) = task {
    let settings =
        ConnectionSettings
            .Create()
            .UseConsoleLogger()
            .SetDefaultUserCredentials(options.UserCredentials)
            .DisableTls()
            .Build()

    let connection = EventStoreConnection.Create(settings, Uri(options.Uri))
    do! connection.ConnectAsync()

    return connection
}

[<CLIMutable>]
type Metadata =
    {
        ApplicationName: string
        Guid: Guid
        //SourceId: SourceId
        EventType: string
        //EventVersion: int
        Timestamp: DateTimeOffset
        TimestampEpoch: int64
        AggregateSequenceNumber: int64
        AggregateId: Guid
        //EventId: EventId
        AggregateName: string
        BatchId: Guid
    }
with
    static member Create (aggregateName, aggregateId) =
        let now = DateTimeOffset.Now
        {
            ApplicationName = ApplicationName
            Guid = Guid.Empty
            EventType = ""
            Timestamp = now
            TimestampEpoch = now |> toUnixTime
            AggregateSequenceNumber = 0L
            AggregateId = aggregateId
            AggregateName = aggregateName
            BatchId = Guid.NewGuid()
        }

let getMetadata (e: ResolvedEvent) : Metadata option =
    e.Event
    |> Option.ofObj
    |> Option.bind (fun x ->
        match x.Metadata with
        | null | [||] -> None
        | arr -> Some(Serialization.deserializeType arr)
    )

let makeRepository<'Event, 'Error> (connection: IEventStoreConnection)
                                   (aggregateName: string)
                                   (serialize: obj -> string * byte array)
                                   (deserialize: Type * string * byte array -> obj) =
    let aggregateStreamId aggregateName (id: Guid) =
        sprintf "%s-%s" aggregateName (id.ToString("N").ToLower())

    let load: LoadAggregateEvents<'Event> =
        (fun (eventType, id) ->
            task {
                let streamId = aggregateStreamId aggregateName id
                let rec readNextPage startFrom pages = task {
                    let! slice = connection.ReadStreamEventsForwardAsync(streamId, startFrom, 4096, false)
                    let pages = slice.Events :: pages
                    if not slice.IsEndOfStream then return! readNextPage slice.NextEventNumber pages else return (slice.LastEventNumber, pages)
                }
                let! version, events = readNextPage 0L []
                let domainEvents = events |> List.rev |> Seq.concat |> Seq.map (fun e -> deserialize(eventType, e.Event.EventType, e.Event.Data)) |> Seq.cast<'Event>
                return (version, domainEvents)
            }
        )

    let commit: CommitAggregateEvents<'Event, 'Error> =
        (fun (id, expectedVersion) (events: 'Event list) ->
            task {
                let streamId = aggregateStreamId aggregateName id
                let batchMetadata = Metadata.Create(aggregateName, id)

                let aggregateSequenceNumber =
                    match expectedVersion with
                    | NewStream -> -1L
                    | Value num -> num

                let eventDatas =
                    events |> List.mapi (fun i e ->
                        let guid = Guid.NewGuid()
                        let eventType, data = serialize e
                        let metadata =
                            { batchMetadata with
                                Guid = guid
                                EventType = eventType
                                AggregateSequenceNumber = aggregateSequenceNumber + 1L + (int64 i)
                            }
                        let _, metadata = serialize metadata
                        EventData(guid, eventType, true, data, metadata)
                    )

                let expectedVersion =
                    match expectedVersion with
                    | NewStream -> int64 ExpectedVersion.NoStream
                    | Value v -> v

                try
                    use! transaction = connection.StartTransactionAsync(streamId, expectedVersion)
                    do! transaction.WriteAsync(eventDatas)
                    let! writeResult = transaction.CommitAsync()

                    return Ok(writeResult.NextExpectedVersion)
                with
                | :? WrongExpectedVersionException ->
                    return Error(WrongExpectedVersion)
                | ex ->
                    return Error(Other ex)
            }
        )

    (load, commit)

let makeDefaultRepository<'Event, 'Error> connection aggregateName =
    makeRepository<'Event, 'Error> connection aggregateName Serialization.serialize Serialization.deserialize
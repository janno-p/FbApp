[<RequireQualifiedAccess>]
module FbApp.Server.EventStore

open EventStore.ClientAPI
open EventStore.ClientAPI.SystemData
open FSharp.Control.Tasks.ContextInsensitive
open Giraffe
open System

let epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)

let toUnixTime (dateTimeOffset: DateTimeOffset) =
    Convert.ToInt64((dateTimeOffset.UtcDateTime - epoch).TotalSeconds);

[<CLIMutable>]
type Metadata =
    {
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
            Guid = Guid.Empty
            EventType = ""
            Timestamp = now
            TimestampEpoch = now |> toUnixTime
            AggregateSequenceNumber = 0L
            AggregateId = aggregateId
            AggregateName = aggregateName
            BatchId = Guid.NewGuid()
        }

let connect () = task {
    let settings =
        ConnectionSettings
            .Create()
            .UseConsoleLogger()
            .SetDefaultUserCredentials(UserCredentials("admin", "changeit"))
            .Build()

    let connection = EventStoreConnection.Create(settings, Uri("tcp://localhost:1113"))
    do! connection.ConnectAsync()

    return connection
}

let makeRepository (connection: IEventStoreConnection)
                   (aggregateName: string)
                   (serialize: obj -> string * byte array)
                   (deserialize: Type * string * byte array -> obj) =
    let streamId (id: Aggregate.Id) = sprintf "%s-%s" aggregateName (id.ToString("N").ToLower())

    let load (eventType, id) = task {
        let streamId = streamId id
        let rec readNextPage pages = task {
            let! slice = connection.ReadStreamEventsForwardAsync(streamId, 1L, 4096, false)
            let pages = slice.Events :: pages
            if not slice.IsEndOfStream then return! readNextPage pages else return pages
        }
        let! events = readNextPage []
        let events = events |> List.rev |> Seq.concat
        let domainEvents = events |> Seq.map (fun e -> deserialize(eventType, e.Event.EventType, e.Event.Data))
        let version = events |> Seq.map (fun e -> e.Event.EventNumber) |> Seq.max
        return (version, domainEvents)
    }

    let commit (id, expectedVersion) (events: 'Event list) = task {
        let streamId = streamId id
        let batchMetadata = Metadata.Create(aggregateName, id)

        let eventDatas =
            events |> List.mapi (fun i e ->
                let guid = Guid.NewGuid()
                let eventType, data = serialize e
                let metadata =
                    { batchMetadata with
                        Guid = guid
                        EventType = eventType
                        AggregateSequenceNumber = expectedVersion + 1L + (int64 i)
                    }
                let _, metadata = serialize metadata
                EventData(guid, eventType, true, data, metadata)
            )

        let expectedVersion =
            match expectedVersion with 0L -> ExpectedVersion.NoStream | v -> v - 1L

        use! transaction = connection.StartTransactionAsync(streamId, expectedVersion)
        do! transaction.WriteAsync(eventDatas)
        let! writeResult = transaction.CommitAsync()

        return writeResult.NextExpectedVersion + 1L
    }

    (load, commit)

let makeReadModelGetter (connection: IEventStoreConnection)
                        (deserialize: byte array -> obj) =
    fun streamId -> task {
        let! slice = connection.ReadStreamEventsBackwardAsync(streamId, -1L, 1, false)
        match slice.Status, slice.Events with
        | SliceReadStatus.Success, [||] ->
            return None
        | SliceReadStatus.Success, [|ev|] when ev.Event.EventNumber = 0L ->
            return None
        | SliceReadStatus.Success, [|ev|] ->
            return Some(deserialize(ev.Event.Data))
        | _ -> return None
    }

let connection = connect().Result

let getMetadata (e: ResolvedEvent) : Metadata option =
    e.Event
    |> Option.ofObj
    |> Option.bind (fun x ->
        match x.Metadata with
        | null | [||] -> None
        | arr -> Some(Serialization.deserializeType arr)
    )

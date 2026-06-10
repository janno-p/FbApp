module FbApp.Api.EventStore

open FSharp.Control
open FbApp.Api.Aggregate
open System
open KurrentDB.Client

let [<Literal>] ApplicationName = "FbApp"

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
        AggregateSequenceNumber: uint64
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
            TimestampEpoch = now.ToUnixTimeSeconds()
            AggregateSequenceNumber = 0UL
            AggregateId = aggregateId
            AggregateName = aggregateName
            BatchId = Guid.NewGuid()
        }

let getMetadata jsonOptions (e: ResolvedEvent) : Metadata option =
    e.Event
    |> Option.ofObj
    |> Option.bind (fun x ->
        match x.Metadata with
        | v when v.IsEmpty -> None
        | arr -> Some(Serialization.deserializeType jsonOptions arr)
    )

let rec readNextPage streamId startFrom (pages: ResizeArray<ResolvedEvent>) (client: KurrentDBClient) = task {
    let result = client.ReadStreamAsync(Direction.Forwards, streamId, startFrom, maxCount=4096L, resolveLinkTos=false)
    match! result.ReadState with
    | ReadState.Ok ->
        let! events = result |> AsyncSeq.toArrayAsync
        pages.AddRange events
        if events.Length = 4096 then
            do! client |> readNextPage streamId events[4095].OriginalEventNumber pages
    | ReadState.StreamNotFound | _ -> ()
}

let makeRepository<'Event, 'Error> (client: KurrentDBClient)
                                   (aggregateName: string)
                                   (serialize: obj -> string * ReadOnlyMemory<byte>)
                                   (deserialize: Type * string * ReadOnlyMemory<byte> -> obj) =
    let aggregateStreamId aggregateName (id: Guid) =
        sprintf "%s-%s" aggregateName (id.ToString("N").ToLower())

    let load: LoadAggregateEvents<'Event> =
        fun (eventType, id) ->
            task {
                let streamId = aggregateStreamId aggregateName id
                let pages = ResizeArray<ResolvedEvent>()
                do! client |> readNextPage streamId StreamPosition.Start pages
                let domainEvents = pages |> Seq.map (fun e -> deserialize(eventType, e.Event.EventType, e.Event.Data)) |> Seq.cast<'Event>
                let eventNumber =
                    if pages.Count > 0 then
                        Some(pages[pages.Count - 1].OriginalEventNumber.ToUInt64())
                    else
                        None
                return eventNumber, domainEvents
            }

    let commit: CommitAggregateEvents<'Event, 'Error> =
        fun (id, expectedVersion) (events: 'Event list) ->
            task {
                let streamId = aggregateStreamId aggregateName id
                let batchMetadata = Metadata.Create(aggregateName, id)

                let aggregateSequenceNumber i =
                    match expectedVersion with
                    | NewStream -> i
                    | Value num -> num + 1UL + i

                let eventDatas =
                    events |> List.mapi (fun i e ->
                        let guid = Guid.NewGuid()
                        let eventType, data = serialize e
                        let metadata =
                            { batchMetadata with
                                Guid = guid
                                EventType = eventType
                                AggregateSequenceNumber = aggregateSequenceNumber (Convert.ToUInt64 i)
                            }
                        let _, metadata = serialize metadata
                        EventData(Uuid.FromGuid guid, eventType, data, metadata)
                    )

                let expectedVersion =
                    match expectedVersion with
                    | NewStream -> StreamState.NoStream
                    | Value v -> StreamState.StreamRevision v

                try
                    let! writeResult = client.AppendToStreamAsync(streamId, expectedVersion, eventDatas)
                    return Ok(writeResult.NextExpectedStreamState.ToInt64())
                with
                | :? WrongExpectedVersionException ->
                    return Error WrongExpectedVersion
                | ex ->
                    return Error(Other ex)
            }

    load, commit

let makeDefaultRepository<'Event, 'Error> connection aggregateName jsonOptions =
    makeRepository<'Event, 'Error> connection aggregateName (Serialization.serialize jsonOptions) (Serialization.deserialize jsonOptions)

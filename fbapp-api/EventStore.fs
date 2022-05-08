module FbApp.Api.EventStore

open EventStore.Client
open FbApp.Api.Aggregate
open System
open System.Collections.Generic

let [<Literal>] ApplicationName = "FbApp"

let epoch = DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)

let toUnixTime (dateTimeOffset: DateTimeOffset) =
    Convert.ToInt64((dateTimeOffset.UtcDateTime - epoch).TotalSeconds);

[<CLIMutable>]
type EventStoreOptions = {
    Uri: string
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
        | v when v.IsEmpty -> None
        | arr -> Some(Serialization.deserializeType arr)
    )

let makeRepository<'Event, 'Error> (client: EventStoreClient)
                                   (aggregateName: string)
                                   (serialize: obj -> string * ReadOnlyMemory<byte>)
                                   (deserialize: Type * string * ReadOnlyMemory<byte> -> obj) =
    let aggregateStreamId aggregateName (id: Guid) =
        sprintf "%s-%s" aggregateName (id.ToString("N").ToLower())

    let readSlice (v: IAsyncEnumerator<ResolvedEvent>) = task {
        let events = ResizeArray<ResolvedEvent>()
        let rec readNext () = task {
            let! hasNext = v.MoveNextAsync()
            if hasNext then
                events.Add(v.Current)
                do! readNext()
        }
        do! readNext()
        return events
    }

    let load: LoadAggregateEvents<'Event> =
        (fun (eventType, id) ->
            task {
                let streamId = aggregateStreamId aggregateName id
                let pages = ResizeArray<ResolvedEvent>()
                let rec readNextPage startFrom = task {
                    let result = client.ReadStreamAsync(Direction.Forwards, streamId, startFrom, maxCount=4096L, resolveLinkTos=false)
                    let! events = readSlice (result.GetAsyncEnumerator())
                    pages.AddRange(events)
                    if events.Count = 4096 then
                        do! readNextPage events.[4095].OriginalEventNumber
                }
                do! readNextPage StreamPosition.Start
                let domainEvents = pages |> Seq.map (fun e -> deserialize(eventType, e.Event.EventType, e.Event.Data)) |> Seq.cast<'Event>
                let eventNumber =
                    if pages.Count > 0 then
                        pages.[pages.Count - 1].OriginalEventNumber.ToInt64()
                    else
                        StreamPosition.Start.ToInt64()
                return (eventNumber, domainEvents)
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
                        EventData(Uuid.FromGuid(guid), eventType, data, metadata)
                    )

                let expectedVersion =
                    match expectedVersion with
                    | NewStream -> StreamRevision.None.ToInt64()
                    | Value v -> v

                try
                    let! writeResult = client.AppendToStreamAsync(streamId, StreamRevision.FromInt64 expectedVersion, eventDatas)
                    return Ok(writeResult.NextExpectedStreamRevision.ToInt64())
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

[<RequireQualifiedAccess>]
module FbApp.Server.EventStore

open EventStore.ClientAPI
open EventStore.ClientAPI.Common.Log
open EventStore.ClientAPI.Projections
open EventStore.ClientAPI.SystemData
open FSharp.Control.Tasks.ContextInsensitive
open Giraffe
open System
open System.Net
open EventStore.ClientAPI.Exceptions

let epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)

let toUnixTime (dateTimeOffset: DateTimeOffset) =
    Convert.ToInt64((dateTimeOffset.UtcDateTime - epoch).TotalSeconds);

let [<Literal>] ApplicationName = "FbApp"
let [<Literal>] DomainEventsStreamName = "domain-events"
let [<Literal>] ProjectionsSubscriptionGroup = "projections"
let [<Literal>] ProcessManagerSubscriptionGroup = "process-manager"

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

let setupEventStore (connection: IEventStoreConnection, options: EventStoreOptions) = task {
    let logger = ConsoleLogger()

    let projectionsManager = ProjectionsManager(logger, IPEndPoint(IPAddress.Loopback, 2113), TimeSpan.FromSeconds(5.0))

    let query = (sprintf """fromAll()
.when({
    $any: function (state, ev) {
        if (ev.metadata !== null && ev.metadata.applicationName === "%s") {
            linkTo("%s", ev)
        }
    }
})""" ApplicationName DomainEventsStreamName)

    let withExceptionLogging f = task {
        try
            do! f()
        with e -> logger.Info(e, e.Message)
    }

    do! withExceptionLogging (fun () -> task {
        do! projectionsManager.CreateContinuousAsync(DomainEventsStreamName, query, options.UserCredentials)
    })

    let settings = PersistentSubscriptionSettings.Create().ResolveLinkTos().StartFromBeginning().Build()

    do! withExceptionLogging (fun () -> task {
        do! connection.CreatePersistentSubscriptionAsync(DomainEventsStreamName, ProjectionsSubscriptionGroup, settings, null)
    })

    do! withExceptionLogging (fun () -> task {
        do! connection.CreatePersistentSubscriptionAsync(DomainEventsStreamName, ProcessManagerSubscriptionGroup, settings, null)
    })
}

let createEventStoreConnection (options: EventStoreOptions) = task {
    let settings =
        ConnectionSettings
            .Create()
            .UseConsoleLogger()
            .SetDefaultUserCredentials(options.UserCredentials)
            .Build()

    let connection = EventStoreConnection.Create(settings, Uri(options.Uri))
    do! connection.ConnectAsync()

    do! setupEventStore (connection, options)

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

let makeRepository<'Event, 'Error> (connection: IEventStoreConnection)
                                   (aggregateName: string)
                                   (serialize: obj -> string * byte array)
                                   (deserialize: Type * string * byte array -> obj) =
    let streamId (id: Guid) = sprintf "%s-%s" aggregateName (id.ToString("N").ToLower())

    let load: Aggregate.LoadAggregateEvents<'Event> =
        (fun (eventType, id) ->
            task {
                let streamId = streamId id
                let rec readNextPage pages = task {
                    let! slice = connection.ReadStreamEventsForwardAsync(streamId, 1L, 4096, false)
                    let pages = slice.Events :: pages
                    if not slice.IsEndOfStream then return! readNextPage pages else return (slice.LastEventNumber, pages)
                }
                let! version, events = readNextPage []
                let domainEvents = events |> List.rev |> Seq.concat |> Seq.map (fun e -> deserialize(eventType, e.Event.EventType, e.Event.Data)) |> Seq.cast<'Event>
                return (version, domainEvents)
            }
        )

    let commit: Aggregate.CommitAggregateEvents<'Event, 'Error> =
        (fun (id, expectedVersion) (events: 'Event list) ->
            task {
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

                try
                    use! transaction = connection.StartTransactionAsync(streamId, expectedVersion)
                    do! transaction.WriteAsync(eventDatas)
                    let! writeResult = transaction.CommitAsync()

                    return Ok(writeResult.NextExpectedVersion + 1L)
                with
                | :? WrongExpectedVersionException ->
                    return Error(Aggregate.WrongExpectedVersion)
                | ex ->
                    return Error(Aggregate.Other ex)
            }
        )

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

let getMetadata (e: ResolvedEvent) : Metadata option =
    e.Event
    |> Option.ofObj
    |> Option.bind (fun x ->
        match x.Metadata with
        | null | [||] -> None
        | arr -> Some(Serialization.deserializeType arr)
    )

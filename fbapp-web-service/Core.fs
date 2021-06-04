namespace FbApp.Core

open XploRe.Util
open System
open FSharp.Control

module Aggregate =
    open FSharp.Control.Tasks
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

    type CommandHandler<'Command, 'Error> = Uuid * ExpectedVersion -> 'Command -> TaskResult<int64, AggregateError<'Error>>
    type LoadAggregateEvents<'Event> = Type * Uuid -> Task<(int64 * 'Event seq)>
    type CommitAggregateEvents<'Event, 'Error> = Uuid * ExpectedCommitVersion -> 'Event list -> TaskResult<int64, AggregateError<'Error>>

    let makeHandler (aggregate: Aggregate<'State, 'Command, 'Event, 'Error>)
                    (load: LoadAggregateEvents<'Event>, commit: CommitAggregateEvents<'Event, 'Error>) : CommandHandler<'Command, 'Error> =
        fun (streamId, expectedVersion) command -> task {
            let! ver, events = load (typeof<'Event>, streamId)
            let state = events |> Seq.fold (fun state event -> Some(aggregate.Evolve state event)) Option<'State>.None
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

module Serialization =
    open FSharp.Reflection
    open Newtonsoft.Json
    open Newtonsoft.Json.Serialization
    open System.IO
    open System.Text

    module Converters =
        // open Giraffe.Common
        open System
        open System.Collections.Generic

        type ListConverter () =
            inherit JsonConverter()
            override __.CanConvert (typ) =
                typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<list<_>>
            override __.WriteJson (writer, value, serializer) =
                let list = value :?> System.Collections.IEnumerable |> Seq.cast
                serializer.Serialize(writer, list)
            override __.ReadJson (reader, typ, _, serializer) =
                let itemType = typ.GetGenericArguments().[0]
                let collectionType = typedefof<IEnumerable<_>>.MakeGenericType(itemType)
                let collection = serializer.Deserialize(reader, collectionType) :?> System.Collections.IEnumerable |> Seq.cast
                let listType = typedefof<list<_>>.MakeGenericType(itemType)
                let cases = FSharpType.GetUnionCases(listType)
                let rec make = function
                    | [] -> FSharpValue.MakeUnion(cases.[0], [||])
                    | head::tail -> FSharpValue.MakeUnion(cases.[1], [| head; make tail |])
                collection |> Seq.toList |> make

        type OptionConverter () =
            inherit JsonConverter()
            override __.CanConvert (typ) =
                typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<option<_>>
            override __.WriteJson (writer, value, serializer) =
                let value =
                    if value |> isNull then null else
                    let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
                    fields.[0]
                serializer.Serialize(writer, value)
            override __.ReadJson(reader, typ, _, serializer) =
                let innerType = typ.GetGenericArguments().[0]
                let innerType =
                    if innerType.IsValueType then typedefof<Nullable<_>>.MakeGenericType([|innerType|])
                    else innerType
                let value = serializer.Deserialize(reader, innerType)
                let cases = FSharpType.GetUnionCases(typ)
                if value |> isNull then FSharpValue.MakeUnion(cases.[0], [||])
                else FSharpValue.MakeUnion(cases.[1], [|value|])

        type TupleArrayConverter () =
            inherit JsonConverter()
            override __.CanConvert (typ) =
                FSharpType.IsTuple(typ)
            override __.WriteJson (writer, value, serializer) =
                let values = FSharpValue.GetTupleFields(value)
                serializer.Serialize(writer, values)
            override __.ReadJson (reader, typ, _, serializer) =
                let advance = reader.Read >> ignore
                let deserialize typ = serializer.Deserialize(reader, typ)
                let itemTypes = FSharpType.GetTupleElements(typ)

                let readElements () =
                    let rec read index acc =
                        match reader.TokenType with
                        | JsonToken.EndArray -> acc
                        | _ ->
                            let value = deserialize itemTypes.[index]
                            advance()
                            read (index + 1) (acc @ [value])
                    advance()
                    read 0 List.empty

                match reader.TokenType with
                | JsonToken.StartArray ->
                    let values = readElements()
                    FSharpValue.MakeTuple(values |> List.toArray, typ)
                | JsonToken.Null ->
                    null
                | _ -> failwith "invalid token"

        type UnionCaseNameConverter () =
            inherit JsonConverter()
            override __.CanConvert (typ) =
                FSharpType.IsUnion(typ) || (typ.DeclaringType |> isNull |> not && FSharpType.IsUnion(typ.DeclaringType))
            override __.WriteJson (writer, value, serializer) =
                let typ = value.GetType()
                let caseInfo, fieldValues = FSharpValue.GetUnionFields(value, typ)
                writer.WriteStartObject()
                writer.WritePropertyName("case")
                writer.WriteValue(caseInfo.Name)
                writer.WritePropertyName("value")
                let value =
                    match fieldValues.Length with
                    | 0 -> null
                    | 1 -> fieldValues.[0]
                    | _ -> fieldValues :> obj
                serializer.Serialize(writer, value)
                writer.WriteEndObject()
            override __.ReadJson (reader, typ, _, serializer) =
                let typ = if FSharpType.IsUnion(typ) then typ else typ.DeclaringType

                let fail () = failwith "Invalid token!"

                let read (token: JsonToken) =
                    if reader.TokenType = token then
                        let value = reader.Value
                        reader.Read() |> ignore
                        Some(value)
                    else None

                let require v =
                    match v with
                    | Some(o) -> o
                    | None -> fail()

                let readProp (n: string) =
                    read JsonToken.PropertyName
                    |> Option.map (fun v -> if (v :?> string) <> n then fail())

                read JsonToken.StartObject |> require |> ignore
                readProp "case" |> require |> ignore

                let case = read JsonToken.String |> require :?> string
                readProp "value" |> ignore

                let caseInfo = FSharpType.GetUnionCases(typ) |> Seq.find (fun c -> c.Name = case)
                let fields = caseInfo.GetFields()

                let args =
                    match fields.Length with
                    | 0 ->
                        read JsonToken.Null |> require |> ignore
                        [||]
                    | 1 ->
                        [|serializer.Deserialize(reader, fields.[0].PropertyType)|]
                    | _ ->
                        let tupleType = FSharpType.MakeTupleType(fields |> Seq.map (fun f -> f.PropertyType) |> Seq.toArray)
                        let tuple = serializer.Deserialize(reader, tupleType)
                        FSharpValue.GetTupleFields(tuple)

                FSharpValue.MakeUnion(caseInfo, args)

    let serializer = JsonSerializer()
    serializer.Converters.Add(Converters.TupleArrayConverter())
    serializer.Converters.Add(Converters.OptionConverter())
    serializer.Converters.Add(Converters.ListConverter())
    serializer.Converters.Add(Converters.UnionCaseNameConverter())
    serializer.ContractResolver <- CamelCasePropertyNamesContractResolver()

    let eventType o =
        let typ = o.GetType()
        let unionType =
            if FSharpType.IsUnion(typ) then Some(typ)
            else if typ.DeclaringType |> isNull |> not && FSharpType.IsUnion(typ.DeclaringType) then Some(typ.DeclaringType)
            else None
        let typeName (typ: System.Type) =
            let name =
                let n = typ.FullName
                n.Substring(n.LastIndexOf(".") + 1)
            name.Replace("+Event", "")
        unionType
        |> Option.fold (fun _ ut ->
            let unionCase = FSharpValue.GetUnionFields(o, typ) |> fst
            sprintf "%s.%s" (typeName unionCase.DeclaringType) unionCase.Name
        ) typ.Name

    let serialize o : string * ReadOnlyMemory<byte> =
        use ms = new MemoryStream()
        (
            use writer = new JsonTextWriter(new StreamWriter(ms))
            serializer.Serialize(writer, o)
        )
        (eventType o, ReadOnlyMemory(ms.ToArray()))

    let deserialize (typ, _, data: ReadOnlyMemory<byte>) =
        use ms = new MemoryStream(data.ToArray())
        use reader = new JsonTextReader(new StreamReader(ms))
        serializer.Deserialize(reader, typ)

    let deserializeOf<'T> (eventType, data) =
        deserialize (typeof<'T>, eventType, data) |> unbox<'T>

    let deserializeType (data: ReadOnlyMemory<byte>) =
        let json = Encoding.UTF8.GetString(data.ToArray())
        JsonConvert.DeserializeObject<'T>(json)

module EventStore =
    open Aggregate
    open FSharp.Control.Tasks
    open System

    let [<Literal>] ApplicationName = "FbApp"

    let epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)

    let toUnixTime (dateTimeOffset: DateTimeOffset) =
        Convert.ToInt64((dateTimeOffset.UtcDateTime - epoch).TotalSeconds);

    type EventStoreConnectionString =
        EventStoreConnectionString of string

    let createEventStoreClient (EventStoreConnectionString uri) =
        let settings = EventStore.Client.EventStoreClientSettings.Create(uri)
        new EventStore.Client.EventStoreClient(settings)

    [<CLIMutable>]
    type Metadata =
        {
            ApplicationName: string
            Uuid: Uuid
            //SourceId: SourceId
            EventType: string
            //EventVersion: int
            Timestamp: DateTimeOffset
            TimestampEpoch: int64
            AggregateSequenceNumber: int64
            AggregateId: Uuid
            //EventId: EventId
            AggregateName: string
            BatchId: Uuid
        }
    with
        static member Create (aggregateName, aggregateId) =
            let now = DateTimeOffset.Now
            {
                ApplicationName = ApplicationName
                Uuid = Uuid.Empty
                EventType = ""
                Timestamp = now
                TimestampEpoch = now |> toUnixTime
                AggregateSequenceNumber = 0L
                AggregateId = aggregateId
                AggregateName = aggregateName
                BatchId = Uuid.NewRandom()
            }

    let getMetadata (e: EventStore.Client.ResolvedEvent) : Metadata option =
        e.Event
        |> Option.ofObj
        |> Option.bind (fun x ->
            match x.Metadata with
            | v when v.IsEmpty -> None
            | arr -> Some(Serialization.deserializeType arr)
        )

    let makeRepository<'Event, 'Error> (client: EventStore.Client.EventStoreClient)
                                       (aggregateName: string)
                                       (serialize: obj -> string * ReadOnlyMemory<byte>)
                                       (deserialize: Type * string * ReadOnlyMemory<byte> -> obj) =
        let aggregateStreamId aggregateName (id: Uuid) =
            sprintf "%s-%s" aggregateName (id.ToString("N").ToLower())

        let readSlice (v: System.Collections.Generic.IAsyncEnumerator<EventStore.Client.ResolvedEvent>) = task {
            let events = ResizeArray<EventStore.Client.ResolvedEvent>()
            let rec readNext () = task {
                let! hasNext = v.MoveNextAsync()
                if hasNext then
                    events.Add(v.Current)
                    do! readNext ()
            }
            do! readNext ()
            return events
        }

        let load: LoadAggregateEvents<'Event> =
            (fun (eventType, id) ->
                task {
                    let streamId = aggregateStreamId aggregateName id
                    let pages = ResizeArray<EventStore.Client.ResolvedEvent>()
                    let rec readNextPage startFrom = unitTask {
                        let slice = client.ReadStreamAsync(EventStore.Client.Direction.Forwards, streamId, startFrom, maxCount=4096L, resolveLinkTos=false)
                        let! events = readSlice slice
                        pages.AddRange(events)
                        if events.Count = 4096 then
                            do! readNextPage events.[4095].OriginalEventNumber
                    }
                    do! readNextPage EventStore.Client.StreamPosition.Start
                    let domainEvents = pages |> Seq.map (fun e -> deserialize(eventType, e.Event.EventType, e.Event.Data)) |> Seq.cast<'Event>
                    return (pages.[pages.Count - 1].OriginalEventNumber.ToInt64(), domainEvents)
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
                            let uuid = Uuid.NewRandom()
                            let eventType, data = serialize e
                            let metadata =
                                { batchMetadata with
                                    Uuid = uuid
                                    EventType = eventType
                                    AggregateSequenceNumber = aggregateSequenceNumber + 1L + (int64 i)
                                }
                            let _, metadata = serialize metadata
                            EventStore.Client.EventData(EventStore.Client.Uuid.FromGuid(uuid.ToGuid()), eventType, data, metadata)
                        )

                    let expectedVersion =
                        match expectedVersion with
                        | NewStream -> EventStore.Client.StreamRevision.None.ToInt64()
                        | Value v -> v

                    try
                        let! writeResult = client.AppendToStreamAsync(streamId, EventStore.Client.StreamRevision.FromInt64 expectedVersion, eventDatas)
                        return Ok(writeResult.NextExpectedStreamRevision.ToInt64())
                    with
                    | :? EventStore.Client.WrongExpectedVersionException ->
                        return Error(WrongExpectedVersion)
                    | ex ->
                        return Error(Other ex)
                }
            )

        (load, commit)

    let makeDefaultRepository<'Event, 'Error> connection aggregateName =
        makeRepository<'Event, 'Error> connection aggregateName Serialization.serialize Serialization.deserialize

[<RequireQualifiedAccess>]
module FootballData =
    open Serialization.Converters
    open FSharp.Control.Tasks
    open Newtonsoft.Json
    open Newtonsoft.Json.Serialization
    open System
    open System.Collections.Generic
    open System.Net.Http

    [<CLIMutable>]
    type Error =
        {
            Error: string
        }

    type Id =
        int64

    [<CLIMutable>]
    type Link =
        {
            Href: string
        }

    let private parseIdSuffix (link: Link) =
        let index = link.Href.LastIndexOf("/")
        let value = link.Href.Substring(index + 1)
        Convert.ToInt64(value)

    [<CLIMutable>]
    type GroupEntry =
        {
            Group: string
            Rank: int
            Team: string
            TeamId: int64
            PlayedGames: int
            CrestURI: string
            Points: int
            Goals: int
            GoalsAgainst: int
            GoalDifference: int
        }

    [<CLIMutable>]
    type LeagueEntryLinks =
        {
            Team: Link
        }

    [<CLIMutable>]
    type LeagueEntryStats =
        {
            Goals: int
            GoalsAgainst: int
            Wins: int
            Draws: int
            Losses: int
        }

    [<CLIMutable>]
    type LeagueEntry =
        {
            [<JsonProperty("_links")>] Links: LeagueEntryLinks
            Position: int
            TeamName: string
            CrestURI: string
            PlayedGames: int
            Points: int
            Goals: int
            GoalsAgainst: int
            GoalDifference: int
            Wins: int
            Draws: int
            Losses: int
            Home: LeagueEntryStats
            Away: LeagueEntryStats
        }

    type CompetitionLeagueTableStandings =
        | Groups of Dictionary<string, GroupEntry array>
        | League of LeagueEntry array

    type private CompetitionLeagueTableConverter () =
        inherit JsonConverter ()
        let expectedType = typeof<CompetitionLeagueTableStandings>
        override __.CanConvert typ =
            expectedType.IsAssignableFrom(typ)
        override __.WriteJson (_,_,_) =
            raise (NotImplementedException())
        override __.ReadJson (reader, typ, _, serializer) =
            match reader.TokenType with
            | JsonToken.StartObject ->
                Groups (serializer.Deserialize<Dictionary<string, GroupEntry array>>(reader)) |> box
            | _ ->
                League (serializer.Deserialize<LeagueEntry array>(reader)) |> box

    [<CLIMutable>]
    type CompetitionLinks =
        {
            Self: Link
            Teams: Link
            Fixtures: Link
            LeagueTable: Link
        }

    [<CLIMutable>]
    type CompetitionTeamsLinks =
        {
            Self: Link
            Competition: Link
        }

    [<CLIMutable>]
    type CompetitionTeamLinks =
        {
            Self: Link
            Fixtures: Link
            Players: Link
        }

    [<CLIMutable>]
    type CompetitionFixturesLinks =
        {
            Self: Link
            Competition: Link
        }

    [<CLIMutable>]
    type CompetitionFixtureLinks =
        {
            Self: Link
            Competition: Link
            HomeTeam: Link
            AwayTeam: Link
        }

    [<CLIMutable>]
    type TeamFixturesLinks =
        {
            Self: Link
            Team: Link
        }

    [<CLIMutable>]
    type TeamPlayersLinks =
        {
            Self: Link
            Team: Link
        }

    [<CLIMutable>]
    type Competition =
        {
            [<JsonProperty("_links")>] Links: CompetitionLinks
            Id: Id
            Caption: string
            League: string
            Year: string
            CurrentMatchday: int
            NumberOfMatchdays: int
            NumberOfTeams: int
            NumberOfGames: int
            LastUpdated: DateTimeOffset
        }

    [<CLIMutable>]
    type CompetitionTeam =
        {
            [<JsonProperty("_links")>] Links: CompetitionTeamLinks
            Name: string
            Code: string
            ShortName: string
            SquadMarketValue: string
            CrestUrl: string
        }
    with
        member this.Id =
            this.Links.Self |> parseIdSuffix

    [<CLIMutable>]
    type CompetitionTeams =
        {
            [<JsonProperty("_links")>] Links: CompetitionTeamsLinks
            Count: int
            Teams: CompetitionTeam array
        }

    [<CLIMutable>]
    type CompetitionLeagueTable =
        {
            LeagueCaption: string
            Matchday: int
            Standings: CompetitionLeagueTableStandings
        }

    [<CLIMutable>]
    type FixtureResultGoals =
        {
            GoalsHomeTeam: int
            GoalsAwayTeam: int
        }

    [<CLIMutable>]
    type FixtureResult =
        {
            GoalsHomeTeam: int option
            GoalsAwayTeam: int option
            HalfTime: FixtureResultGoals option
            ExtraTime: FixtureResultGoals option
            PenaltyShootout: FixtureResultGoals option
        }

    [<CLIMutable>]
    type CompetitionFixture =
        {
            [<JsonProperty("_links")>] Links: CompetitionFixtureLinks
            Date: DateTimeOffset
            Status: string
            Matchday: int
            HomeTeamName: string
            AwayTeamName: string
            Result: FixtureResult option
            //Odds
        }
    with
        member this.Id =
            this.Links.Self |> parseIdSuffix
        member this.HomeTeamId =
            this.Links.HomeTeam |> parseIdSuffix
        member this.AwayTeamId =
            this.Links.AwayTeam |> parseIdSuffix

    [<CLIMutable>]
    type CompetitionFixtures =
        {
            [<JsonProperty("_links")>] Links: CompetitionFixturesLinks
            Count: int
            Fixtures: CompetitionFixture array
        }

    [<CLIMutable>]
    type Fixtures =
        {
            TimeFrameStart: DateTimeOffset
            TimeFrameEnd: DateTimeOffset
            Count: int
            Fixtures: CompetitionFixture array
        }

    [<CLIMutable>]
    type HeadToHead =
        {
            Count: int
            TimeFrameStart: DateTimeOffset
            TimeFrameEnd: DateTimeOffset
            HomeTeamWins: int
            AwayTeamWins: int
            Draws: int
            LastHomeWinHomeTeam: CompetitionFixture option
            LasWinHomeTeam: CompetitionFixture option
            LastAwayWinAwayTeam: CompetitionFixture option
            LastWinAwayTeam: CompetitionFixture option
            Fixtures: CompetitionFixture array
        }

    [<CLIMutable>]
    type Fixture =
        {
            Fixture: CompetitionFixture
            [<JsonProperty("head2head")>] HeadToHead: HeadToHead
        }

    [<CLIMutable>]
    type TeamFixtures =
        {
            [<JsonProperty("_links")>] Links: TeamFixturesLinks
            Season: int
            Count: int
            Fixtures: CompetitionFixture array
        }

    [<CLIMutable>]
    type TeamPlayer =
        {
            Name: string
            Position: string
            JerseyNumber: int
            DateOfBirth: DateTimeOffset
            Nationality: string
            ContractUntil: DateTimeOffset option
            MarketValue: string
        }

    [<CLIMutable>]
    type TeamPlayers =
        {
            [<JsonProperty("_links")>] Links: TeamPlayersLinks
            Count: int
            Players: TeamPlayer array
        }

    type CompetitionFilter =
        | Season of int
    with
        override this.ToString() =
            match this with
            | Season year -> sprintf "season=%d" year

    type LeagueTableFilter =
        | Matchday of int
    with
        override this.ToString() =
            match this with
            | Matchday day -> sprintf "matchday=%d" day

    type TimeFrame =
        | Previous of int
        | Next of int
    with
        override this.ToString() =
            match this with
            | Previous v -> sprintf "p%d" v
            | Next v -> sprintf "n%d" v

    type CompetitionFixtureFilter =
        | TimeFrame of TimeFrame
        | TimeFrameRange of DateTimeOffset * DateTimeOffset
        | Matchday of int
    with
        override this.ToString() =
            match this with
            | TimeFrame tf -> sprintf "timeFrame=%s" (tf.ToString())
            | TimeFrameRange (s, e) -> sprintf "timeFrameStart=%s&timeFrameEnd=%s" (s.ToString("yyyy-MM-dd")) (e.ToString("yyyy-MM-dd"))
            | Matchday n -> sprintf "matchday=%d" n

    type FixturesFilter =
        | TimeFrame of TimeFrame
        | League of string
    with
        override this.ToString() =
            match this with
            | TimeFrame tf -> sprintf "timeFrame=%s" (tf.ToString())
            | League name -> sprintf "league=%s" name

    type FixtureFilter =
        | HeadToHead of int
    with
        override this.ToString() =
            match this with
            | HeadToHead n -> sprintf "head2head=%d" n

    type Venue =
        | Home
        | Away
    with
        override this.ToString() =
            match this with
            | Home -> "home"
            | Away -> "away"

    type TeamFixturesFilter =
        | Season of int
        | TimeFrame of TimeFrame
        | Venue of Venue
    with
        override this.ToString() =
            match this with
            | Season year -> sprintf "season=%d" year
            | TimeFrame tf -> sprintf "timeFrame=%s" (tf.ToString())
            | Venue v -> sprintf "venue=%s" (v.ToString())

    let serializer = JsonSerializer()
    serializer.Converters.Add(OptionConverter())
    serializer.Converters.Add(CompetitionLeagueTableConverter())
    serializer.ContractResolver <- CamelCasePropertyNamesContractResolver()

    let deserialize<'T> (stream: IO.Stream) =
        use reader = new JsonTextReader(new IO.StreamReader(stream))
        serializer.Deserialize(reader, typeof<'T>) |> unbox<'T>

    let private toQuery lst =
        match lst with
        | [] -> ""
        | xs -> String.Join("&", xs |> List.map (fun x -> x.ToString())) |> sprintf "?%s"

    let private baseUri =
        Uri("http://api.football-data.org/v1/")

    let private createClient (authToken: string) =
        let client = new HttpClient()
        client.BaseAddress <- baseUri
        client.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
        client

    let private apiCall<'T> authToken (uri: string) = task {
        use client = createClient authToken
        let! response = client.GetAsync(uri)
        if response.IsSuccessStatusCode then
            let! jsonStream = response.Content.ReadAsStreamAsync()
            return Ok(deserialize<'T> jsonStream)
        else
            let! jsonStream = response.Content.ReadAsStreamAsync()
            return Error(response.StatusCode, response.ReasonPhrase, deserialize<Error> jsonStream)
    }

    /// List all available competitions.
    let getCompetitions authToken (filters: CompetitionFilter list) = task {
        let uri = sprintf "competitions%s" (filters |> toQuery)
        return! apiCall<Competition array> authToken uri
    }

    /// List all teams for a certain competition.
    let getCompetitionTeams authToken (competitionId: Id) = task {
        let uri = sprintf "competitions/%d/teams" competitionId
        return! apiCall<CompetitionTeams> authToken uri
    }

    /// Show league table / current standing.
    let getCompetitionLeagueTable authToken (competitionId: Id) (filters: LeagueTableFilter list) = task {
        let uri = sprintf "competitions/%d/leagueTable%s" competitionId (filters |> toQuery)
        return! apiCall<CompetitionLeagueTable> authToken uri
    }

    /// List all fixtures for a certain competition.
    let getCompetitionFixtures authToken (competitionId: Id) (filters: CompetitionFixtureFilter list) = task {
        let uri = sprintf "competitions/%d/fixtures%s" competitionId (filters |> toQuery)
        return! apiCall<CompetitionFixtures> authToken uri
    }

    /// List fixtures across a set of competitions.
    let getFixtures authToken (filters: FixturesFilter list) = task {
        let uri = sprintf "fixtures%s" (filters |> toQuery)
        return! apiCall<Fixtures> authToken uri
    }

    /// Show one fixture.
    let getFixture authToken (fixtureId: Id) (filters: FixtureFilter list) = task {
        let uri = sprintf "fixtures/%d%s" fixtureId (filters |> toQuery)
        return! apiCall<Fixture> authToken uri
    }

    /// Show all fixtures for a certain team.
    let getTeamFixtures authToken (teamId: Id) (filters: TeamFixturesFilter list) = task {
        let uri = sprintf "teams/%d/fixtures%s" teamId (filters |> toQuery)
        return! apiCall<TeamFixtures> authToken uri
    }

    /// Show one team.
    let getTeam authToken (teamId: Id) = task {
        let uri = sprintf "teams/%d" teamId
        return! apiCall<CompetitionTeam> authToken uri
    }

    /// Show all players for a certain team.
    let getTeamPlayers authToken (teamId: Id) = task {
        let uri = sprintf "teams/%d/players" teamId
        return! apiCall<TeamPlayers> authToken uri
    }

    module Api2 =
        let private baseUri =
            Uri("http://api.football-data.org/v2/")

        let private createClient (authToken: string) =
            let client = new HttpClient()
            client.BaseAddress <- baseUri
            client.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
            client

        let private apiCall<'T> authToken (uri: string) = task {
            use client = createClient authToken
            let! response = client.GetAsync(uri)
            if response.IsSuccessStatusCode then
                let! jsonStream = response.Content.ReadAsStreamAsync()
                return Ok(deserialize<'T> jsonStream)
            else
                let! jsonStream = response.Content.ReadAsStreamAsync()
                return Error(response.StatusCode, response.ReasonPhrase, deserialize<Error> jsonStream)
        }
        type CompetitionMatchFilter =
            | DateRange of DateTimeOffset * DateTimeOffset
            | Stage of string
            | Status of string
            | Matchday of int
            | Group of string
        with
            override this.ToString() =
                match this with
                | DateRange (dateFrom, dateTo) -> sprintf "dateFrom=%s&dateTo=%s" (dateFrom.ToString("yyyy-MM-dd")) (dateTo.ToString("yyyy-MM-dd"))
                | Stage stage -> sprintf "stage=%s" stage
                | Status status -> sprintf "status=%s" status
                | Matchday matchday -> sprintf "matchday=%d" matchday
                | Group group -> sprintf "group=%s" group

        [<CLIMutable>]
        type CompetitionSeason =
            {
                Id: Id
                StartDate: DateTimeOffset
                EndDate: DateTimeOffset
                CurrentMatchday: int
            }

        [<CLIMutable>]
        type Resource =
            {
                Id: Id
                Name: string
            }

        [<CLIMutable>]
        type CompetitionMatchScoreGoals =
            {
                HomeTeam: int option
                AwayTeam: int option
            }

        [<CLIMutable>]
        type CompetitionMatchScore =
            {
                Winner: string option
                Duration: string
                FullTime: CompetitionMatchScoreGoals
                HalfTime: CompetitionMatchScoreGoals
                ExtraTime: CompetitionMatchScoreGoals
                Penalties: CompetitionMatchScoreGoals
            }

        [<CLIMutable>]
        type CompetitionMatchReferee =
            {
                Id: Id
                Name: string
                Nationality: string
            }

        [<CLIMutable>]
        type CompetitionMatch =
            {
                Id: Id
                Competition: Resource
                Season: CompetitionSeason
                UtcDate: DateTimeOffset
                Status: string
                Matchday: int option
                Stage: string
                Group: string
                LastUpdated: DateTimeOffset
                HomeTeam: Resource
                AwayTeam: Resource
                Score: CompetitionMatchScore
                Referees: CompetitionMatchReferee array
            }

        [<CLIMutable>]
        type CompetitionMatches =
            {
                Count: int
                Season: CompetitionSeason
                Matches: CompetitionMatch array
                // filters
            }

        let getCompetitionMatches authToken (competitionId: Id) (filters: CompetitionMatchFilter list) = task {
            let uri = sprintf "competitions/%d/matches%s" competitionId (filters |> toQuery)
            return! apiCall<CompetitionMatches> authToken uri
        }

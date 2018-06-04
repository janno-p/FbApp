module FbApp.Server.Dashboard

open EventStore.ClientAPI
open EventStore.ClientAPI
open EventStore.ClientAPI
open FSharp.Control.Tasks
open FSharp.Control.Tasks
open FbApp.Server.Common
open FSharp.Control.Tasks.ContextInsensitive
open FbApp.Server
open FbApp.Server
open FbApp.Server
open FbApp.Server
open Giraffe
open Newtonsoft.Json
open Saturn
open System
open System.Net.Http

[<CLIMutable>]
type CompetitionData =
    {
        Id: int
        Caption: string
        League: string
        Year: string
        CurrentMatchday: int
        NumberOfMatchdays: int
        NumberOfTeams: int
        NumberOfGames: int
        LastUpdated: DateTime
    }

type CompetitionItem =
    {
        Label: string
        Value: int
    }

type CompetitionDto =
    {
        Description: string
        ExternalSource: int64
    }

let connection =
    EventStore.connect().Result

type CompetitionReadModel =
    {
        Id: Guid
        Description: string
        ExternalSource: int64
    }

let competitions = System.Collections.Generic.Dictionary<Guid, CompetitionReadModel>()

let eventAppeared (x: EventStoreCatchUpSubscription) (e: ResolvedEvent) : System.Threading.Tasks.Task = upcast task {
    let md: EventStore.Metadata = Serialization.deserializeType e.Event.Metadata
    match Serialization.deserializeOf<Competition.Event> (e.Event.EventType, e.Event.Data) with
    | Competition.Created args ->
        competitions.Add(
            md.AggregateId,
            {
                Id = md.AggregateId
                Description = args.Description
                ExternalSource = args.ExternalSource
            }
        )
}

let liveProcessingStarted (_: EventStoreCatchUpSubscription) =
    ()

let subscriptionDropped (_: EventStoreCatchUpSubscription) (_: SubscriptionDropReason) (_: exn)=
    ()

connection.SubscribeToStreamFrom("$ce-Competition", Nullable(), CatchUpSubscriptionSettings.Default, eventAppeared, liveProcessingStarted, subscriptionDropped)
|> ignore

(*
let get =
    let get = EventStore.makeReadModelGetter connection (fun data -> Serialization.deserializeType<CompetitionReadModel>(data))
    fun (id: Guid) -> get (sprintf "CompetitionReadModel-%s" (id.ToString("N")))
*)

let handleCommand =
    Aggregate.makeHandler
        { InitialState = Competition.initialState; Decide = Competition.decide; Evolve = Competition.evolve }
        (EventStore.makeRepository connection "Competition" Serialization.serialize Serialization.deserialize)

let getCompetitionSources year: HttpHandler =
    (fun next context ->
        task {
            use client = new HttpClient()
            let! json = client.GetStringAsync(sprintf "https://www.football-data.org/v1/competitions?season=%d" year)
            let competitions =
                JsonConvert.DeserializeObject<CompetitionData[]>(json)
                |> Array.map (fun x -> { Label = sprintf "%s (%s)" x.Caption x.League; Value = x.Id })
            return! Successful.OK competitions next context
        })

let addCompetition: HttpHandler =
    (fun next context ->
        task {
            let! dto = context.BindJsonAsync<CompetitionDto>()
            let command = Competition.Create(dto.Description, dto.ExternalSource)
            let id = Guid.NewGuid()
            let! result = handleCommand (id, 0L) command
            match result with
            | Ok(_) -> return! Successful.ACCEPTED (id.ToString("N")) next context
            | Error(_) -> return! RequestErrors.BAD_REQUEST "" next context
        })

let getCompetitions: HttpHandler =
    (fun next context ->
        task {
            let competitions = competitions.Values |> Seq.sortBy (fun x -> x.Description) |> Seq.toList
            return! Successful.OK competitions next context
        })

let dashboardScope = scope {
    get "/competitions" getCompetitions
    getf "/competition_sources/%i" getCompetitionSources
    post "/competition/add" addCompetition
}

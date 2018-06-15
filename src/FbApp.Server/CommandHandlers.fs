[<RequireQualifiedAccess>]
module FbApp.Server.CommandHandlers

open FbApp.Core.Aggregate
open FbApp.Domain

type CompetitionHandler = CommandHandler<Competitions.Id, Competitions.Command, unit>
let mutable competitionHandler = Unchecked.defaultof<CompetitionHandler>

type PredictionHandler = CommandHandler<Predictions.Id, Predictions.Command, unit>
let mutable predictionHandler = Unchecked.defaultof<PredictionHandler>

type FixturesHandler = CommandHandler<Fixtures.Id, Fixtures.Command, unit>
let mutable fixturesHandler = Unchecked.defaultof<FixturesHandler>

[<RequireQualifiedAccess>]
module FbApp.Server.CommandHandlers

open FbApp.Core.Aggregate
open FbApp.Domain

type CompetitionHandler = CommandHandler<Competitions.Id, Competitions.Command, unit>
let mutable competitionsHandler = Unchecked.defaultof<CompetitionHandler>

type PredictionHandler = CommandHandler<Predictions.Id, Predictions.Command, unit>
let mutable predictionsHandler = Unchecked.defaultof<PredictionHandler>

type FixturesHandler = CommandHandler<Fixtures.Id, Fixtures.Command, Fixtures.Error>
let mutable fixturesHandler = Unchecked.defaultof<FixturesHandler>

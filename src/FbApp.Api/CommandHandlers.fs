[<RequireQualifiedAccess>]
module FbApp.Api.CommandHandlers

open FbApp.Api.Aggregate
open FbApp.Api.Domain

type CompetitionHandler = CommandHandler<Competitions.Command, unit>
let mutable competitionsHandler = Unchecked.defaultof<CompetitionHandler>

type PredictionHandler = CommandHandler<Predictions.Command, unit>
let mutable predictionsHandler = Unchecked.defaultof<PredictionHandler>

type FixturesHandler = CommandHandler<Fixtures.Command, Fixtures.Error>
let mutable fixturesHandler = Unchecked.defaultof<FixturesHandler>

type LeaguesHandler = CommandHandler<Leagues.Command, unit>
let mutable leaguesHandler = Unchecked.defaultof<LeaguesHandler>

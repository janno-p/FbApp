[<RequireQualifiedAccess>]
module FbApp.Server.Aggregate

open FbApp.Core.Aggregate

module Handlers =
    type CompetitionHandler = CommandHandler<Competition.Id, Competition.Command, unit>
    let mutable competitionHandler = Unchecked.defaultof<CompetitionHandler>

    type PredictionHandler = CommandHandler<Prediction.Id, Prediction.Command, unit>
    let mutable predictionHandler = Unchecked.defaultof<PredictionHandler>

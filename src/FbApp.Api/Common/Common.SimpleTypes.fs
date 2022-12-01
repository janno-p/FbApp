module FbApp.Common.SimpleTypes

open System
open Be.Vlaanderen.Basisregisters.Generators.Guid
open FbApp.Api.Domain


type CompetitionId =
    private CompetitionId of Guid


type FixtureId =
    private FixtureId of Guid


type PlayerId =
    private PlayerId of int64


type PredictionId =
    private PredictionId of Guid


type TeamId =
    private TeamId of int64


module CompetitionId =
    let private competitionsNamespace =
        Guid "1dc53967-8c3b-49a9-9496-27a2267bbef7"

    let create (externalId: int64) =
        Deterministic.Create(competitionsNamespace, externalId.ToString(), 5)
            |> CompetitionId

    let fromGuid =
        CompetitionId

    let value (CompetitionId guid) =
        guid


module FixtureId =
    let private fixturesNamespace =
        Guid "2130666a-7b4b-44c7-9d0a-da020138ffc0"

    let create (competitionId: CompetitionId) (externalId: int64) =
        let competitionIdValue = (CompetitionId.value competitionId).ToString("N")
        Deterministic.Create(fixturesNamespace, sprintf "%s-%s" competitionIdValue (externalId.ToString()), 5)
            |> FixtureId

    let fromGuid =
        FixtureId

    let value (FixtureId guid) =
        guid


module PlayerId =
    let create (externalId: int64) =
        PlayerId externalId


module PredictionId =
    let private predictionsNamespace =
        Guid "2945d861-0b2f-4783-914b-97988b98c76b"

    let create (competitionId: CompetitionId) (Predictions.Email email) =
        let competitionIdValue = (CompetitionId.value competitionId).ToString("N")
        Deterministic.Create(predictionsNamespace, (sprintf "%s-%s" competitionIdValue email), 5)
            |> PredictionId

    let fromGuid =
        PredictionId

    let value (PredictionId guid) =
        guid


module TeamId =
    let create (externalId: int64) =
        TeamId externalId

    let value (TeamId v) =
        v

module FbApp.Common.SimpleTypes

open System
open Be.Vlaanderen.Basisregisters.Generators.Guid


type CompetitionId =
    private CompetitionId of Guid


module CompetitionId =
    let private competitionsNamespace =
        Guid "1dc53967-8c3b-49a9-9496-27a2267bbef7"

    let create (externalId: int64) =
        Deterministic.Create(competitionsNamespace, externalId.ToString(), 5)
        |> CompetitionId

    let value (CompetitionId guid) =
        guid

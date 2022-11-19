namespace FbApp.Competition.Dto

open System
open FbApp.Competition.Queries


type CompetitionStatusDto = {
    StartDate: int64 Nullable
    Description: string
    Status: string
}


module CompetitionStatusDto =

    let mapCompetitionStatus (competition: Competition option) =
        match competition with
        | Some c when c.Date < DateTimeOffset.Now ->
            "in-progress"
        | Some c ->
            "accept-predictions"
        | None ->
            "not-active"


    let fromCompetition (competition: Competition option) =
        {
            CompetitionStatusDto.StartDate =
                competition |> Option.map (fun c -> c.Date.ToUnixTimeMilliseconds()) |> Option.toNullable
            Description =
                competition |> Option.map (fun c -> c.Description) |> Option.toObj
            Status =
                mapCompetitionStatus competition
        }

module FbApp.PredictionResults.Dto

open FbApp.Common
open FbApp.PredictionResults.ReadModel


type PredictionResultDto = {
    Name: string
    Points: int array
    Total: int
    Rank: int
    Ratio: double
}


module PredictionResultDto =

    let fromScoresheet (scoresheet: Scoresheet) =
        let gainedPoints = [
            1 * (scoresheet.GroupStage.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            2 * (scoresheet.Qualifiers.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            3 * (scoresheet.Quarters.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            4 * (scoresheet.Semis.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            5 * (scoresheet.Final.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            6 * (match snd scoresheet.Winner with Some true -> 1 | _ -> 0)
            5 * (if scoresheet.Scorer |> List.exists (fun x -> x.IsTopScorer = Final(true)) then 1 else 0)
            1 * (scoresheet.Scorer |> List.map _.GoalCount |> List.sum)
        ]
        let lostPoints = [
            1 * (scoresheet.GroupStage.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            2 * (scoresheet.Qualifiers.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            3 * (scoresheet.Quarters.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            4 * (scoresheet.Semis.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            5 * (scoresheet.Final.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            6 * (match snd scoresheet.Winner with Some false -> 1 | _ -> 0)
            5 * (if scoresheet.Scorer |> Seq.forall (fun x -> x.IsTopScorer = Final(false)) then 1 else 0)
            0
        ]
        {
            Name = Helpers.excludeLastName scoresheet.Name
            Points = gainedPoints |> Array.ofList
            Total = gainedPoints |> List.sum
            Ratio = double (MaxTotalPoints - (List.sum lostPoints)) / double MaxTotalPoints * 100.0
            Rank = 0
        }

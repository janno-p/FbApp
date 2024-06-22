module FbApp.PredictionResults.Dto

open FbApp.Common
open FbApp.PredictionResults.ReadModel


type PredictionResultDto = {
    Name: string
    Points: int array
    ScorerFixed: bool
    Total: int
    Rank: int
    Ratio: double
}


module PredictionResultDto =
    let private getTopScorer (ss: Scoresheet) =
        if ss.Scorer |> List.isEmpty then
            (false, false)
        else if ss.Scorer |> List.forall (fun x -> x.IsTopScorer = Final(false)) then
            (false, true)
        else if ss.Scorer |> List.exists (fun x -> x.IsTopScorer = Final(true)) then
            (true, true)
        else if ss.Scorer |> List.exists (fun x -> x.IsTopScorer = Pending(true)) then
            (true, false)
        else
            (false, false)

    let fromScoresheet (scoresheet: Scoresheet) =
        let topScorer, topScorerIsFinal = getTopScorer scoresheet
        let topScorerGoalPoints = scoresheet.Scorer |> List.map _.GoalCount |> List.sum
        let gainedPoints = [
            1 * (scoresheet.GroupStage.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            2 * (scoresheet.Qualifiers.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            3 * (scoresheet.Quarters.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            4 * (scoresheet.Semis.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            5 * (scoresheet.Final.Values |> Seq.filter ((=) (Some true)) |> Seq.length)
            6 * (match snd scoresheet.Winner with Some true -> 1 | _ -> 0)
            5 * (if topScorer then 1 else 0)
            1 * topScorerGoalPoints
        ]
        let lostPoints = [
            1 * (scoresheet.GroupStage.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            2 * (scoresheet.Qualifiers.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            3 * (scoresheet.Quarters.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            4 * (scoresheet.Semis.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            5 * (scoresheet.Final.Values |> Seq.filter ((=) (Some false)) |> Seq.length)
            6 * (match snd scoresheet.Winner with Some false -> 1 | _ -> 0)
            5 * (if not topScorer && topScorerIsFinal then 1 else 0)
            0
        ]
        let maxTotalPoints = MaxTotalPoints + topScorerGoalPoints
        {
            Name = Helpers.excludeLastName scoresheet.Name
            Points = gainedPoints |> Array.ofList
            ScorerFixed = topScorerIsFinal
            Total = gainedPoints |> List.sum
            Ratio = double (maxTotalPoints - (List.sum lostPoints)) / double maxTotalPoints * 100.0
            Rank = 0
        }

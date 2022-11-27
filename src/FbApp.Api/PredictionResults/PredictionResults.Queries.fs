module FbApp.PredictionResults.Queries

open System
open System.Threading.Tasks
open FbApp.Common.SimpleTypes
open MongoDB.Driver


type QualifiersResult = {
    Id: int64
    HasQualified: Nullable<bool>
}


type PredictionFixtureResult = {
    FixtureId: int64
    PredictedResult: string
    ActualResult: string
}


type ScorerResult = {
    Id: int64
    IsCorrect: bool
}


type Prediction = {
    Id: Guid
    Name: string
    Email: string
    CompetitionId: Guid
    Fixtures: PredictionFixtureResult array
    QualifiersRoundOf16: QualifiersResult array
    QualifiersRoundOf8: QualifiersResult array
    QualifiersRoundOf4: QualifiersResult array
    QualifiersRoundOf2: QualifiersResult array
    TopScorers: ScorerResult array
    Winner: QualifiersResult
    Leagues: Guid array
    Version: int64
}


type PredictionResult = {
    Id: Guid
    Name: string
    Points: double array
    Total: double
    Ratio: double
    Rank: int32
}


module AggregationStage =
    let findByCompetitionId (competitionId: CompetitionId) =
        sprintf @"{ $match: { CompetitionId: { $eq: UUID(""%A"") } } }" (CompetitionId.value competitionId)

    let fixPredictedResult = """
        { $addFields: {
          Fixtures: {
            $map: {
              input: "$Fixtures",
              in: {
                "$mergeObjects": [
                  "$$this", {
                    PredictedResult: {
                      $switch: {
                        branches: [
                          { case: { "$eq": [ "$$this.PredictedResult", "AwayWin"] }, then: "HomeWin" },
                          { case: { "$eq": [ "$$this.PredictedResult", "HomeWin"] }, then: "AwayWin" },
                        ],
                        default: "Tie"
                      }
                    }
                  }
                ]
              }
            }
          }
        } }
        """


type GetLeaderboard = IMongoDatabase -> CompetitionId -> Task<PredictionResult ResizeArray>


let getLeaderboard : GetLeaderboard =
    fun db competitionId -> task {
        let predictions = db.GetCollection<Prediction>("predictions")
        return! predictions.Aggregate(
            PipelineDefinition<Prediction, PredictionResult>.Create(
                AggregationStage.findByCompetitionId competitionId,
                AggregationStage.fixPredictedResult,
                """{ $addFields: {
                    q32: { $sum: { $map: { input: "$Fixtures", as: "f", in: { $cond: { if: { $eq: [ "$$f.PredictedResult", "$$f.ActualResult" ] }, then: 1, else: 0 } } } } },
                    c32: { $sum: { $map: { input: "$Fixtures", as: "f", in: { $cond: { if: { $ne: [ "$$f.ActualResult", null ] }, then: 1, else: 0 } } } } },
                    q16: { $sum: { $map: { input: "$QualifiersRoundOf16", as: "q", in: { $cond: { if: "$$q.HasQualified", then: 2, else: 0 } } } } },
                    c16: { $sum: { $map: { input: "$QualifiersRoundOf16", as: "q", in: { $cond: { if: { $ne: [ "$$q.HasQualified", null ] }, then: 2, else: 0 } } } } },
                    q8: { $sum: { $map: { input: "$QualifiersRoundOf8", as: "q", in: { $cond: { if: "$$q.HasQualified", then: 3, else: 0 } } } } },
                    c8: { $sum: { $map: { input: "$QualifiersRoundOf8", as: "q", in: { $cond: { if: { $ne: [ "$$q.HasQualified", null ] }, then: 3, else: 0 } } } } },
                    q4: { $sum: { $map: { input: "$QualifiersRoundOf4", as: "q", in: { $cond: { if: "$$q.HasQualified", then: 4, else: 0 } } } } },
                    c4: { $sum: { $map: { input: "$QualifiersRoundOf4", as: "q", in: { $cond: { if: { $ne: [ "$$q.HasQualified", null ] }, then: 4, else: 0 } } } } },
                    q2: { $sum: { $map: { input: "$QualifiersRoundOf2", as: "q", in: { $cond: { if: "$$q.HasQualified", then: 5, else: 0 } } } } },
                    c2: { $sum: { $map: { input: "$QualifiersRoundOf2", as: "q", in: { $cond: { if: { $ne: [ "$$q.HasQualified", null ] }, then: 5, else: 0 } } } } },
                    q1: { $cond: { if: "$Winner.HasQualified", then: 6, else: 0 } },
                    c1: { $cond: { if: { $ne: ["$Winner.HasQualified", null] }, then: 6, else: 0 } }
                } }""",
                """{ $addFields: {
                    Points: ["$q32", "$q16", "$q8", "$q4", "$q2", "$q1"]
                } }""",
                """{ $addFields: {
                    Total: { $sum: "$Points" },
                    max: { $sum: ["$c32", "$c16", "$c8", "$c4", "$c2", "$c1"] }
                } }""",
                """{ $addFields: {
                    Ratio: { $multiply: [ 100.0, { $divide: [ "$Total", "$max" ] } ] }
                } }""",
                """{ $sort: { Total: -1, Ratio: -1, _id: 1 } }""",
                """{ $setWindowFields: { sortBy: { Total: -1 }, output: { Rank: { $rank: { } } } } }""",
                """{ $project: { Rank: 1, Name: 1, Points: 1, Total: 1, Ratio: 1 } }"""
            )
        ).ToListAsync()
    }

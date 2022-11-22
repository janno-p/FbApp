module FbApp.Fixtures.Queries

open System
open System.Threading.Tasks
open MongoDB.Bson
open MongoDB.Driver


type Team = {
    Name: string
    Code: string
    FlagUrl: string
    ExternalId: int64
}


type FixtureResultPrediction = {
    PredictionId: Guid
    Name: string
    Result: string
}


type QualificationPrediction = {
    PredictionId: Guid
    Name: string
    HomeQualifies: bool
    AwayQualifies: bool
}


type Fixture = {
    Id: Guid
    CompetitionId: Guid
    Date: DateTimeOffset
    PreviousId: Nullable<Guid>
    NextId: Nullable<Guid>
    HomeTeam: Team
    AwayTeam: Team
    Status: string
    FullTime: int array
    ExtraTime: int array
    Penalties: int array
    ResultPredictions: FixtureResultPrediction array
    QualificationPredictions: QualificationPrediction array
    ExternalId: int64
    Stage: string
    Version: int64
}


type GetFixtureById = IMongoDatabase -> Guid -> Task<Fixture option>
type GetNearestFixtureId = IMongoDatabase -> Task<Guid>


let getFixtureById : GetFixtureById =
    fun db fixtureId -> task {
        let! fixture =
            db.GetCollection<Fixture>("fixtures")
                .Find(Builders<Fixture>.Filter.Eq((fun x -> x.Id), fixtureId))
                .SingleOrDefaultAsync()
        return (if fixture |> box |> isNull then None else Some fixture)
    }


let getNearestFixtureId : GetNearestFixtureId =
    fun db -> task {
        let pipelines =
            let now = DateTimeOffset.UtcNow.Ticks
            PipelineDefinition<_,BsonDocument>.Create(
                $"""{{
                    $addFields: {{
                        rank: {{
                            $let: {{
                                vars: {{
                                    diffStart: {{ $abs: {{ $subtract: [ %d{now}, {{ $arrayElemAt: [ "$Date", 0 ] }} ] }} }},
                                    diffEnd: {{ $abs: {{ $subtract: [ %d{now}, {{ $sum: [ 63000000000, {{ $arrayElemAt: [ "$Date", 0 ] }} ] }} ] }} }}
                                }},
                                in: {{ $cond: {{ if: {{ $eq: [ "$Status", "IN_PLAY" ] }}, then: -1, else: {{ $multiply: [ 1, {{ $min: ["$$diffStart", "$$diffEnd" ] }} ] }} }} }}
                            }}
                        }}
                    }}
                }}""",
                """{ $sort: { rank: 1 } }""",
                """{ $limit: 1 }""",
                """{ $project: { _id: 1 } }"""
            )

        let! fixture =
            db.GetCollection<BsonDocument>("fixtures")
                .Aggregate(pipelines)
                .SingleAsync()

        return fixture["_id"].AsGuid
    }

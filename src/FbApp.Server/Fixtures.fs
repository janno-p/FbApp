module FbApp.Server.Fixtures

open Giraffe
open MongoDB.Driver
open Saturn
open System

[<CLIMutable>]
type TeamDto =
    {
        Name: string
        FlagUrl: string
    }
with
    static member FromProjection(team: Projection.Projections.Team) =
        { Name = team.Name; FlagUrl = team.FlagUrl }

[<CLIMutable>]
type FixturePredictionDto =
    {
        Name: string
        Result: string
    }
with
    static member FromProjection(prediction: Projection.Projections.FixturePrediction) =
        let fixName (name: string) =
            name.Split([|' '|], 2).[0]
        { Name = fixName prediction.Name; Result = prediction.Result }

[<CLIMutable>]
type FixtureDto =
    {
        Id: Guid
        Date: DateTimeOffset
        PreviousFixtureId: Nullable<Guid>
        NextFixtureId: Nullable<Guid>
        HomeTeam: TeamDto
        AwayTeam: TeamDto
        Status: string
        HomeGoals: Nullable<int>
        AwayGoals: Nullable<int>
        Predictions: FixturePredictionDto[]
    }
with
    static member FromProjection (fixture: Projection.Projections.Fixture) =
        {
            Id = fixture.Id
            Date = fixture.Date.ToLocalTime()
            PreviousFixtureId = fixture.PreviousId
            NextFixtureId = fixture.NextId
            HomeTeam = TeamDto.FromProjection(fixture.HomeTeam)
            AwayTeam = TeamDto.FromProjection(fixture.AwayTeam)
            Status = fixture.Status
            HomeGoals = fixture.HomeGoals
            AwayGoals = fixture.AwayGoals
            Predictions = fixture.Predictions |> Array.map FixturePredictionDto.FromProjection |> Array.sortBy (fun u -> u.Name.ToLowerInvariant())
        }

let getFixture (id: Guid) : HttpHandler =
    (fun next ctx -> task {
        let filter =
            Projection.FixturesBuilder.Filter.Eq((fun x -> x.Id), id)
        let! fixture =
            Projection.fixtures.Find(filter).SingleAsync()
        let dto =
            FixtureDto.FromProjection(fixture)
        return! Successful.OK dto next ctx
    })

[<CLIMutable>]
type FixtureStatusDto =
    {
        Status: string
        HomeGoals: Nullable<int>
        AwayGoals: Nullable<int>
    }

let getFixtureStatus (id: Guid) : HttpHandler =
    (fun next ctx -> task {
        let filter =
            Projection.FixturesBuilder.Filter.Eq((fun x -> x.Id), id)
        let projection =
            ProjectionDefinition<Projection.Projections.Fixture, FixtureStatusDto>.op_Implicit
                """{ Status: 1, HomeGoals: 1, AwayGoals: 1, _id: 0 }"""
        let! dto =
            Projection.fixtures.Find(filter).Project(projection).SingleAsync()
        return! Successful.OK dto next ctx
    })

let getTimelyFixture : HttpHandler =
    (fun next ctx -> task {
        let pipelines =
            let now = System.DateTimeOffset.UtcNow.Ticks
            PipelineDefinition<_,Projection.Projections.Fixture>.Create(
                (sprintf """{
                    $addFields: {
                        rank: {
                            $let: {
                                vars: {
                                    diffStart: { $abs: { $subtract: [ %d, { $arrayElemAt: [ "$Date", 0 ] } ] } },
                                    diffEnd: { $abs: { $subtract: [ %d, { $sum: [ 63000000000, { $arrayElemAt: [ "$Date", 0 ] } ] } ] } }
                                },
                                in: { $cond: { if: { $eq: [ "$Status", "IN_PLAY" ] }, then: -1, else: { $multiply: [ 1, { $min: ["$$diffStart", "$$diffEnd" ] } ] } } }
                            }
                        }
                    }
                }""" now now),
                """{ $sort: { rank: 1 } }""",
                """{ $limit: 1 }""",
                """{ $addFields: { rank: "$$REMOVE" } }"""
            )
        let! fixture =
            Projection.fixtures.Aggregate(pipelines).SingleAsync()
        let dto =
            FixtureDto.FromProjection(fixture)
        return! Successful.OK dto next ctx
    })

let scope = scope {
    getf "/%O" getFixture
    getf "/%O/status" getFixtureStatus
    get "/timely" getTimelyFixture
}

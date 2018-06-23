﻿module FbApp.Server.Repositories

open FSharp.Control.Tasks.ContextInsensitive
open MongoDB.Bson
open MongoDB.Driver
open System
open System.Collections.Generic

BsonDefaults.GuidRepresentation <- GuidRepresentation.Standard

let mongo = MongoClient()
let db = mongo.GetDatabase("fbapp")

let toBsonGuid (guid: Guid) =
    Convert.ToBase64String(guid.ToByteArray())

module FindFluent =
    let trySingleAsync (x: IFindFluent<_,_>) = task {
        let! result = x.SingleOrDefaultAsync()
        return (if result |> box |> isNull then None else Some(result))
    }

module ReadModels =
    type Team =
        {
            Name: string
            Code: string
            FlagUrl: string
            ExternalId: int64
        }

    type CompetitionFixture =
        {
            HomeTeamId: int64
            AwayTeamId: int64
            Date: DateTimeOffset
            ExternalId: int64
        }

    type Competition =
        {
            Id: Guid
            Description: string
            ExternalId: int64
            Teams: Team[]
            Fixtures: CompetitionFixture[]
            Groups: IDictionary<string, int64[]>
            Version: int64
            Date: DateTimeOffset
        }

    type FixturePrediction =
        {
            PredictionId: Guid
            Name: string
            Result: string
        }

    type Fixture =
        {
            Id: Guid
            CompetitionId: Guid
            Date: DateTimeOffset
            PreviousId: Nullable<Guid>
            NextId: Nullable<Guid>
            HomeTeam: Team
            AwayTeam: Team
            Status: string
            HomeGoals: Nullable<int>
            AwayGoals: Nullable<int>
            Predictions: FixturePrediction[]
            Version: int64
        }

    type PredictionFixtureResult =
        {
            FixtureId: int64
            Result: string
        }

    type PredictionFixture =
        {
            Id: Guid
            Name: string
            Fixtures: PredictionFixtureResult[]
        }

    type Prediction =
        {
            Id: Guid
            Name: string
            Email: string
            CompetitionId: Guid
            Fixtures: PredictionFixtureResult[]
            QualifiersRoundOf16: int64[]
            QualifiersRoundOf8: int64[]
            QualifiersRoundOf4: int64[]
            QualifiersRoundOf2: int64[]
            Winner: int64
            Version: int64
        }

    type League =
        {
            Id: Guid
        }

    type FixtureStatus =
        {
            Status: string
            HomeGoals: Nullable<int>
            AwayGoals: Nullable<int>
        }

    type FixtureOrder =
        {
            Id: Guid
            PreviousId: Nullable<Guid>
            NextId: Nullable<Guid>
        }

module Competitions =
    type Builders = Builders<ReadModels.Competition>
    type FieldDefinition = FieldDefinition<ReadModels.Competition>

    let private collection = db.GetCollection<ReadModels.Competition>("competitions")

    let private filterByIdAndVersion (id, ver) =
        Builders.Filter.Where(fun x -> x.Id = id && x.Version = ver - 1L)

    let getActive () = task {
        let f = Builders.Filter.Eq((fun x -> x.ExternalId), 467L)
        return! collection.Find(f).Limit(Nullable(1)).SingleAsync()
    }

    let get (competitionId: Guid) = task {
        let f = Builders.Filter.Eq((fun x -> x.Id), competitionId)
        return! collection.Find(f).Limit(Nullable(1)) |> FindFluent.trySingleAsync
    }

    let insert competition = task {
        let! _ = collection.InsertOneAsync(competition)
        ()
    }

    let updateTeams (id, version) (teams: ReadModels.Team []) = task {
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Teams), teams)
        let! _ = collection.UpdateOneAsync(filterByIdAndVersion (id, version), u)
        ()
    }

    let updateFixtures (id, version) (fixtures: ReadModels.CompetitionFixture []) = task {
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Fixtures), fixtures)
        let! _ = collection.UpdateOneAsync(filterByIdAndVersion (id, version), u)
        ()
    }

    let updateGroups (id, version) (groups: IDictionary<_,_>) = task {
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Groups), groups)
        let! _ = collection.UpdateOneAsync(filterByIdAndVersion (id, version), u)
        ()
    }

    let getAll () = task {
        let sort = Builders.Sort.Ascending(FieldDefinition.op_Implicit("Description"))
        return! collection.Find(Builders.Filter.Empty).Sort(sort).ToListAsync()
    }

module Fixtures =
    open FbApp.Domain

    type Builders = Builders<ReadModels.Fixture>
    type FieldDefinition = FieldDefinition<ReadModels.Fixture>
    type ProjectionDefinition<'T> = ProjectionDefinition<ReadModels.Fixture, 'T>

    let private collection = db.GetCollection<ReadModels.Fixture>("fixtures")

    let insert fixture = task {
        let! _ = collection.InsertOneAsync(fixture)
        ()
    }

    let updateScore (id, version) (homeGoals, awayGoals) = task {
        let f = Builders.Filter.Eq((fun x -> x.Id), id)
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.HomeGoals), Nullable(homeGoals))
                    .Set((fun x -> x.AwayGoals), Nullable(awayGoals))
        let! _ = collection.UpdateOneAsync(f, u)
        ()
    }

    let updateStatus (id, version) (status: Fixtures.FixtureStatus) = task {
        let f = Builders.Filter.Eq((fun x -> x.Id), id)
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Status), status.ToString())
        let! _ = collection.UpdateOneAsync(f, u)
        ()
    }

    let addPrediction id (prediction: ReadModels.FixturePrediction) = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let update = Builders.Update.Push(FieldDefinition.op_Implicit "Predictions", prediction)
        let! _ = collection.UpdateOneAsync(idFilter, update)
        ()
    }

    let get id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        return! collection.Find(idFilter).SingleAsync()
    }

    let getFixtureStatus id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let projection = ProjectionDefinition<ReadModels.FixtureStatus>.op_Implicit """{ Status: 1, HomeGoals: 1, AwayGoals: 1, _id: 0 }"""
        return! collection.Find(idFilter).Project(projection).SingleAsync()
    }

    let getTimelyFixture () = task {
        let pipelines =
            let now = System.DateTimeOffset.UtcNow.Ticks
            PipelineDefinition<_,ReadModels.Fixture>.Create(
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
        return! collection.Aggregate(pipelines).SingleAsync()
    }

    let getFixtureOrder competitionId = task {
        let filter = Builders.Filter.Eq((fun x -> x.CompetitionId), competitionId)
        let projection = ProjectionDefinition<ReadModels.FixtureOrder>.op_Implicit """{ Id: 1, PreviousId: 1, NextId: 1 }"""
        let sort =
            Builders.Sort.Combine(
                Builders.Sort.Ascending(FieldDefinition<_>.op_Implicit "Date.0"),
                Builders.Sort.Ascending(FieldDefinition<_>.op_Implicit "Id")
            )
        return! collection.Find(filter).Sort(sort).Project(projection).ToListAsync()
    }

    let setAdjacentFixtures id (previousId, nextId) = task {
        let previousId = previousId |> Option.toNullable
        let nextId = nextId |> Option.toNullable
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let update = Builders.Update.Set((fun x -> x.PreviousId), previousId).Set((fun x -> x.NextId), nextId)
        let! _ = collection.UpdateOneAsync(idFilter, update)
        ()
    }

module Predictions =
    type Builders = Builders<ReadModels.Prediction>

    let private collection = db.GetCollection<ReadModels.Prediction>("predictions")

    let ofFixture (competitionId: Guid) (externalFixtureId: int64) = task {
        let pipelines =
            PipelineDefinition<ReadModels.Prediction, ReadModels.PredictionFixture>.Create(
                (sprintf
                    """{ $match: { CompetitionId: { $eq: new BinData(4, "%s") }, "Fixtures.FixtureId": { $eq: %d } } }"""
                    (toBsonGuid competitionId)
                    externalFixtureId),
                (sprintf
                    """{ $project: { Name: 1, Fixtures: { $filter: { input: "$Fixtures", as: "fixture", cond: { $eq: ["$$fixture.FixtureId", %d] } } } } }"""
                    externalFixtureId)
            )
        return! collection.Aggregate(pipelines).ToListAsync()
    }

    let private idToGuid (competitionId, email) =
        (competitionId, FbApp.Domain.Predictions.Email email) |> FbApp.Domain.Predictions.Id |> FbApp.Domain.Predictions.streamId

    let delete id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let! _ = collection.DeleteOneAsync(idFilter)
        ()
    }

    let insert prediction = task {
        let! _ = collection.InsertOneAsync(prediction)
        ()
    }

    let get id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), (idToGuid id))
        return! collection.Find(idFilter) |> FindFluent.trySingleAsync
    }

module Leagues =
    let private collection = db.GetCollection<ReadModels.League>("leagues")
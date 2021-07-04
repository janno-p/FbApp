module FbApp.Api.Repositories

open FSharp.Control.Tasks
open MongoDB.Bson
open MongoDB.Driver
open System
open System.Collections.Generic
open System.Linq

let private settings =
    MongoClientSettings(
        GuidRepresentation = GuidRepresentation.Standard,
        Server = MongoServerAddress("mongo")
    )

let private mongo = MongoClient(settings)
let private db = mongo.GetDatabase("fbapp")

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
            Teams: Team array
            Fixtures: CompetitionFixture array
            Groups: IDictionary<string, int64 array>
            Version: int64
            Date: DateTimeOffset
        }

    type FixtureResultPrediction =
        {
            PredictionId: Guid
            Name: string
            Result: string
        }

    type QualificationPrediction =
        {
            PredictionId: Guid
            Name: string
            HomeQualifies: bool
            AwayQualifies: bool
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
            FullTime: int array
            ExtraTime: int array
            Penalties: int array
            ResultPredictions: FixtureResultPrediction array
            QualificationPredictions: QualificationPrediction array
            ExternalId: int64
            Stage: string
            Version: int64
        }

    type PredictionFixtureResult =
        {
            FixtureId: int64
            PredictedResult: string
            ActualResult: string
        }

    type PredictionFixture =
        {
            Id: Guid
            Name: string
            Fixtures: PredictionFixtureResult array
        }

    type QualifiersResult =
        {
            Id: int64
            HasQualified: Nullable<bool>
        }

    type PredictionQualifier =
        {
            Id: Guid
            Name: string
            Qualifiers: QualifiersResult array
        }

    type Prediction =
        {
            Id: Guid
            Name: string
            Email: string
            CompetitionId: Guid
            Fixtures: PredictionFixtureResult array
            QualifiersRoundOf16: QualifiersResult array
            QualifiersRoundOf8: QualifiersResult array
            QualifiersRoundOf4: QualifiersResult array
            QualifiersRoundOf2: QualifiersResult array
            Winner: QualifiersResult
            Leagues: Guid array
            Version: int64
        }

    type League =
        {
            Id: Guid
            CompetitionId: Guid
            Name: string
            Code: string
        }

    type FixtureStatus =
        {
            Status: string
            FullTime: int array
            ExtraTime: int array
            Penalties: int array
        }

    type FixtureOrder =
        {
            Id: Guid
            PreviousId: Nullable<Guid>
            NextId: Nullable<Guid>
        }

    type PredictionItem =
        {
            Id: Guid
            Name: string
        }

    type FixtureTeamId = { TeamId: int64 }

    type PredictionScore =
        {
            Id: Guid
            Name: string
            Points: double array
            Total: double
            Ratio: double
        }

module Competitions =
    type Builders = Builders<ReadModels.Competition>
    type FieldDefinition = FieldDefinition<ReadModels.Competition>

    let private collection = db.GetCollection<ReadModels.Competition>("competitions")

    let private filterByIdAndVersion (id, ver) =
        Builders.Filter.Where(fun x -> x.Id = id && x.Version = ver - 1L)

    let tryGetActive () = task {
        let f = Builders.Filter.Eq((fun x -> x.ExternalId), 2018L)
        return! collection.Find(f).Limit(Nullable(1)) |> FindFluent.trySingleAsync
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
    open FbApp.Api.Domain

    type Builders = Builders<ReadModels.Fixture>
    type FieldDefinition = FieldDefinition<ReadModels.Fixture>
    type ProjectionDefinition<'T> = ProjectionDefinition<ReadModels.Fixture, 'T>

    let private collection = db.GetCollection<ReadModels.Fixture>("fixtures")

    let insert fixture = task {
        let! _ = collection.InsertOneAsync(fixture)
        ()
    }

    let updateScore (id, version) (fullTime: Fixtures.FixtureGoals, extraTime: Fixtures.FixtureGoals option, penalties: Fixtures.FixtureGoals option) = task {
        let f = Builders.Filter.Eq((fun x -> x.Id), id)
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.FullTime), [| fullTime.Home; fullTime.Away |])
                    .Set((fun x -> x.ExtraTime), extraTime |> Option.fold (fun _ u -> [| u.Home; u.Away |]) null)
                    .Set((fun x -> x.Penalties), penalties |> Option.fold (fun _ u -> [| u.Home; u.Away |]) null)
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

    let addPrediction id (prediction: ReadModels.FixtureResultPrediction) = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let update = Builders.Update.Push(FieldDefinition.op_Implicit "ResultPredictions", prediction)
        let! _ = collection.UpdateOneAsync(idFilter, update)
        ()
    }

    let get id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        return! collection.Find(idFilter).SingleAsync()
    }

    let getFixtureStatus id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let projection = ProjectionDefinition<ReadModels.FixtureStatus>.op_Implicit """{ Status: 1, FullTime: 1, ExtraTime: 1, Penalties: 1, _id: 0 }"""
        return! collection.Find(idFilter).Project(projection).SingleAsync()
    }

    let getTimelyFixture () = task {
        let pipelines =
            let now = DateTimeOffset.UtcNow.Ticks
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

    let getFixtureCount (competitionId: Guid, stage: string) = task {
        return! collection.CountDocumentsAsync(FilterDefinition.op_Implicit (sprintf """{ CompetitionId: CSUUID("%O"), Stage: { $eq: "%s" } }""" competitionId stage))
    }

    let getQualifiedTeams (competitionId: Guid) = task {
        let! teams =
            collection.Aggregate(
                PipelineDefinition<_,ReadModels.FixtureTeamId>.Create(
                    (sprintf """{ $match: { CompetitionId: CSUUID("%O"), Stage: "LAST_16" } }""" competitionId),
                    """{ $project: { _id: 0, TeamId: ["$HomeTeam.ExternalId", "$AwayTeam.ExternalId"] } }""",
                    """{ $unwind: "$TeamId" }"""
                )
            ).ToListAsync<ReadModels.FixtureTeamId>()
        return teams |> Seq.map (fun x -> x.TeamId) |> Seq.toArray
    }

module Predictions =
    open FbApp.Api.Domain

    type Builders = Builders<ReadModels.Prediction>
    type FieldDefinition = FieldDefinition<ReadModels.Prediction>
    type ProjectionDefinition<'T> = ProjectionDefinition<ReadModels.Prediction, 'T>

    let private collection = db.GetCollection<ReadModels.Prediction>("predictions")

    let ofFixture (competitionId: Guid) (externalFixtureId: int64) = task {
        let pipelines =
            PipelineDefinition<ReadModels.Prediction, ReadModels.PredictionFixture>.Create(
                (sprintf """{ $match: { CompetitionId: { $eq: CSUUID("%O") }, "Fixtures.FixtureId": { $eq: %d } } }""" competitionId externalFixtureId),
                (sprintf """{ $project: { Name: 1, Fixtures: { $filter: { input: "$Fixtures", as: "fixture", cond: { $eq: ["$$fixture.FixtureId", %d] } } } } }""" externalFixtureId)
            )
        return! collection.Aggregate(pipelines).ToListAsync()
    }

    let ofStage (competitionId: Guid, stage: string) =
        match stage with
        | "LAST_16"
        | "QUARTER_FINAL"
        | "SEMI_FINAL" ->
            task {
                let qualName = if stage = "LAST_16" then "QualifiersRoundOf8" elif stage = "QUARTER_FINAL" then "QualifiersRoundOf4" else "QualifiersRoundOf2"
                let pipelines =
                    PipelineDefinition<ReadModels.Prediction, ReadModels.PredictionQualifier>.Create(
                        (sprintf """{ $match: { CompetitionId: { $eq: CSUUID("%O") } } }""" competitionId),
                        (sprintf """{ $project: { Name: 1, Qualifiers: "$%s" } }""" qualName)
                    )
                return! collection.Aggregate(pipelines).ToListAsync()
            }
        | "FINAL" ->
            task {
                let pipelines =
                    PipelineDefinition<ReadModels.Prediction, ReadModels.PredictionQualifier>.Create(
                        (sprintf """{ $match: { CompetitionId: { $eq: CSUUID("%O") } } }""" competitionId),
                        """{ $project: { Name: 1, Qualifiers: ["$Winner"] } }"""
                    )
                return! collection.Aggregate(pipelines).ToListAsync()
            }
        | _ -> task { return ResizeArray<_>() }

    let private idToGuid (competitionId, email) =
        Predictions.createId (competitionId, Predictions.Email email)

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

    let getById id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        return! collection.Find(idFilter).SingleAsync()
    }

    let find (competitionId: Guid) (term: string) = task {
        let filter =
            Builders.Filter.And(
                Builders.Filter.Eq((fun x -> x.CompetitionId), competitionId),
                Builders.Filter.Regex(FieldDefinition.op_Implicit "Name", BsonRegularExpression(term, "i"))
            )
        let project =
            ProjectionDefinition<ReadModels.PredictionItem>.op_Implicit """{ _id: 1, Name: 1 }"""
        return! collection.Find(filter).Project(project).ToListAsync()
    }

    let addToLeague (predictionId, leagueId) = task {
        let filter = Builders.Filter.Eq((fun x -> x.Id), predictionId)
        let update = Builders.Update.AddToSet(FieldDefinition.op_Implicit "Leagues", leagueId)
        let! _ = collection.UpdateOneAsync(filter, update)
        ()
    }

    let updateResult (competitionId, fixtureId: int64, actualResult) = task {
        let! _ = 
            collection.UpdateManyAsync(
                (fun x -> x.CompetitionId = competitionId && x.Fixtures.Any(fun y -> y.FixtureId = fixtureId)),
                Builders.Update.Set((fun x -> x.Fixtures.ElementAt(-1).ActualResult), actualResult)
            )
        ()
    }

    let private getColName = function
        | "GROUP_STAGE" -> "QualifiersRoundOf16"
        | "LAST_16" -> "QualifiersRoundOf8"
        | "QUARTER_FINAL" -> "QualifiersRoundOf4"
        | "SEMI_FINAL" -> "QualifiersRoundOf2"
        | "FINAL" -> "Winner"
        | x -> failwithf "Unexpected value: `%s`" x

    let updateQualifiers (competitionId: Guid, stage: string, teamId: int64, hasQualified: bool) = task {
        let colName = getColName stage
        let! _ =
            collection.UpdateManyAsync(
                FilterDefinition.op_Implicit (sprintf """{ CompetitionId: CSUUID("%O"), "%s._id": %d }""" competitionId colName teamId),
                UpdateDefinition.op_Implicit (sprintf """{ $set: { "%s.$.HasQualified": %b } }""" colName hasQualified)
            )
        ()
    }

    let setUnqualifiedTeams (competitionId: Guid, teams: int64 array) = task {
        let teamList = String.Join(",", teams)

        let updateQualifiers name = task {
            let! _ =
                collection.UpdateManyAsync(
                    FilterDefinition.op_Implicit (sprintf """{ CompetitionId: CSUUID("%O") }""" competitionId),
                    UpdateDefinition.op_Implicit (sprintf """{ $set: { "%s.$[q].HasQualified": false } }""" name),
                    UpdateOptions(
                        ArrayFilters = [
                            ArrayFilterDefinition<ReadModels.Prediction>.op_Implicit (sprintf """{ $and: [ { "q.HasQualified": { $eq: null } }, { "q._id": { $in: [%s] } } ] }""" teamList)
                        ]
                    )
                )
            ()
        }

        do! updateQualifiers "QualifiersRoundOf16"
        do! updateQualifiers "QualifiersRoundOf8"
        do! updateQualifiers "QualifiersRoundOf4"
        do! updateQualifiers "QualifiersRoundOf2"

        let! _ =
            collection.UpdateManyAsync(
                FilterDefinition.op_Implicit (sprintf """{ CompetitionId: CSUUID("%O"), "Winner.HasQualified": { $eq: null }, "Winner._id": { $in: [%s] } }""" competitionId teamList),
                UpdateDefinition.op_Implicit """{ $set: { "Winner.HasQualified": false } }"""
            )
        ()
    }

    let getScoreTable (competitionId: Guid) = task {
        return! collection.Aggregate(
            PipelineDefinition<ReadModels.Prediction, ReadModels.PredictionScore>.Create(
                (sprintf """{ $match: { CompetitionId: { $eq: CSUUID("%O") } } }""" competitionId),
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
                """{ $project: { Name: 1, Points: 1, Total: 1, Ratio: 1 } }"""
            )
        ).ToListAsync()
    }

module Leagues =
    let private collection = db.GetCollection<ReadModels.League>("leagues")

    type Builders = Builders<ReadModels.League>
    type FieldDefinition = FieldDefinition<ReadModels.League>

    let getAll () = task {
        let sort = Builders.Sort.Ascending(FieldDefinition.op_Implicit "Name")
        return! collection.Find(Builders.Filter.Empty).Sort(sort).ToListAsync()
    }

    let insert league = task {
        let! _ = collection.InsertOneAsync(league)
        ()
    }

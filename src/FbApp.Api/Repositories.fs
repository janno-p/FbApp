module FbApp.Api.Repositories

open MongoDB.Bson
open MongoDB.Driver
open System
open System.Collections.Generic
open System.Linq

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

    type CompetitionPlayer =
        {
            Name: string
            Position: string
            TeamExternalId: int64
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
            Players: CompetitionPlayer array
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

    type ScorerResult =
        {
            Id: int64
            IsCorrect: bool
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
            TopScorers: ScorerResult array
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

    let private getCollection (db: IMongoDatabase) =
        db.GetCollection<ReadModels.Competition>("competitions")

    let private filterByIdAndVersion (id, ver) =
        Builders.Filter.Where(fun x -> x.Id = id && x.Version = ver - 1L)

    let tryGetActive db = task {
        let f = Builders.Filter.Eq((fun x -> x.ExternalId), FootballData.ActiveCompetition)
        return! (getCollection db).Find(f).Limit(Nullable(1)) |> FindFluent.trySingleAsync
    }

    let get db (competitionId: Guid) = task {
        let f = Builders.Filter.Eq((fun x -> x.Id), competitionId)
        return! (getCollection db).Find(f).Limit(Nullable(1)) |> FindFluent.trySingleAsync
    }

    let insert db competition = task {
        let! _ = (getCollection db).InsertOneAsync(competition)
        ()
    }

    let updatePlayers db (id, version) (players: ReadModels.CompetitionPlayer []) = task {
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Players), players)
        let! _ = (getCollection db).UpdateOneAsync(filterByIdAndVersion (id, version), u)
        ()
    }

    let updateTeams db (id, version) (teams: ReadModels.Team []) = task {
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Teams), teams)
        let! _ = (getCollection db).UpdateOneAsync(filterByIdAndVersion (id, version), u)
        ()
    }

    let updateFixtures db (id, version) (fixtures: ReadModels.CompetitionFixture []) = task {
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Fixtures), fixtures)
        let! _ = (getCollection db).UpdateOneAsync(filterByIdAndVersion (id, version), u)
        ()
    }

    let updateGroups db (id, version) (groups: IDictionary<_,_>) = task {
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Groups), groups)
        let! _ = (getCollection db).UpdateOneAsync(filterByIdAndVersion (id, version), u)
        ()
    }

    let getAll db = task {
        let sort = Builders.Sort.Ascending(FieldDefinition.op_Implicit("Description"))
        return! (getCollection db).Find(Builders.Filter.Empty).Sort(sort).ToListAsync()
    }

module Fixtures =
    open FbApp.Api.Domain

    type Builders = Builders<ReadModels.Fixture>
    type FieldDefinition = FieldDefinition<ReadModels.Fixture>
    type ProjectionDefinition<'T> = ProjectionDefinition<ReadModels.Fixture, 'T>

    let private getCollection (db: IMongoDatabase) =
        db.GetCollection<ReadModels.Fixture>("fixtures")

    let insert db fixture = task {
        let! _ = (getCollection db).InsertOneAsync(fixture)
        ()
    }

    let updateScore db (id, version) (fullTime: Fixtures.FixtureGoals, extraTime: Fixtures.FixtureGoals option, penalties: Fixtures.FixtureGoals option) = task {
        let f = Builders.Filter.Eq((fun x -> x.Id), id)
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.FullTime), [| fullTime.Home; fullTime.Away |])
                    .Set((fun x -> x.ExtraTime), extraTime |> Option.fold (fun _ u -> [| u.Home; u.Away |]) null)
                    .Set((fun x -> x.Penalties), penalties |> Option.fold (fun _ u -> [| u.Home; u.Away |]) null)
        let! _ = (getCollection db).UpdateOneAsync(f, u)
        ()
    }

    let updateStatus db (id, version) (status: Fixtures.FixtureStatus) = task {
        let f = Builders.Filter.Eq((fun x -> x.Id), id)
        let u = Builders.Update
                    .Set((fun x -> x.Version), version)
                    .Set((fun x -> x.Status), status.ToString())
        let! _ = (getCollection db).UpdateOneAsync(f, u)
        ()
    }

    let addPrediction db id (prediction: ReadModels.FixtureResultPrediction) = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let update = Builders.Update.Push(FieldDefinition.op_Implicit "ResultPredictions", prediction)
        let! _ = (getCollection db).UpdateOneAsync(idFilter, update)
        ()
    }

    let get db id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        return! (getCollection db).Find(idFilter).SingleAsync()
    }

    let getFixtureStatus db id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let projection = ProjectionDefinition<ReadModels.FixtureStatus>.op_Implicit """{ Status: 1, FullTime: 1, ExtraTime: 1, Penalties: 1, _id: 0 }"""
        return! (getCollection db).Find(idFilter).Project(projection).SingleAsync()
    }

    let getTimelyFixture db = task {
        let pipelines =
            let now = DateTimeOffset.UtcNow.Ticks
            PipelineDefinition<_,ReadModels.Fixture>.Create(
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
                """{ $addFields: { rank: "$$REMOVE" } }"""
            )
        return! (getCollection db).Aggregate(pipelines).SingleAsync()
    }

    let getFixtureOrder db competitionId = task {
        let filter = Builders.Filter.Eq((fun x -> x.CompetitionId), competitionId)
        let projection = ProjectionDefinition<ReadModels.FixtureOrder>.op_Implicit """{ Id: 1, PreviousId: 1, NextId: 1 }"""
        let sort =
            Builders.Sort.Combine(
                Builders.Sort.Ascending(FieldDefinition<_>.op_Implicit "Date.0"),
                Builders.Sort.Ascending(FieldDefinition<_>.op_Implicit "Id")
            )
        return! (getCollection db).Find(filter).Sort(sort).Project(projection).ToListAsync()
    }

    let setAdjacentFixtures db id (previousId, nextId) = task {
        let previousId = previousId |> Option.toNullable
        let nextId = nextId |> Option.toNullable
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let update = Builders.Update.Set((fun x -> x.PreviousId), previousId).Set((fun x -> x.NextId), nextId)
        let! _ = (getCollection db).UpdateOneAsync(idFilter, update)
        ()
    }

    let getFixtureCount db (competitionId: Guid, stage: string) = task {
        return! (getCollection db).CountDocumentsAsync(FilterDefinition.op_Implicit $"""{{ CompetitionId: UUID("{competitionId:N}"), Stage: {{ $eq: "%s{stage}" }} }}""")
    }

    let getQualifiedTeams db (competitionId: Guid) = task {
        let! teams =
            (getCollection db).Aggregate(
                PipelineDefinition<_,ReadModels.FixtureTeamId>.Create(
                    $"""{{ $match: {{ CompetitionId: UUID("{competitionId:N}"), Stage: "LAST_16" }} }}""",
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

    let private getCollection (db: IMongoDatabase) =
        db.GetCollection<ReadModels.Prediction>("predictions")

    let ofFixture db (competitionId: Guid) (externalFixtureId: int64) = task {
        let pipelines =
            PipelineDefinition<ReadModels.Prediction, ReadModels.PredictionFixture>.Create(
                $"""{{ $match: {{ CompetitionId: {{ $eq: UUID("{competitionId:N}") }}, "Fixtures.FixtureId": {{ $eq: %d{externalFixtureId} }} }} }}""",
                $"""{{ $project: {{ Name: 1, Fixtures: {{ $filter: {{ input: "$Fixtures", as: "fixture", cond: {{ $eq: ["$$fixture.FixtureId", %d{externalFixtureId}] }} }} }} }} }}"""
            )
        return! (getCollection db).Aggregate(pipelines).ToListAsync()
    }

    let ofStage db (competitionId: Guid, stage: string) =
        match stage with
        | "LAST_16"
        | "QUARTER_FINALS"
        | "SEMI_FINALS" ->
            task {
                let qualName = if stage = "LAST_16" then "QualifiersRoundOf8" elif stage = "QUARTER_FINALS" then "QualifiersRoundOf4" else "QualifiersRoundOf2"
                let pipelines =
                    PipelineDefinition<ReadModels.Prediction, ReadModels.PredictionQualifier>.Create(
                        $"""{{ $match: {{ CompetitionId: {{ $eq: UUID("{competitionId:N}") }} }} }}""",
                        $"""{{ $project: {{ Name: 1, Qualifiers: "$%s{qualName}" }} }}"""
                    )
                return! (getCollection db).Aggregate(pipelines).ToListAsync()
            }
        | "FINAL" ->
            task {
                let pipelines =
                    PipelineDefinition<ReadModels.Prediction, ReadModels.PredictionQualifier>.Create(
                        $"""{{ $match: {{ CompetitionId: {{ $eq: UUID("{competitionId:N}") }} }} }}""",
                        """{ $project: { Name: 1, Qualifiers: ["$Winner"] } }"""
                    )
                return! (getCollection db).Aggregate(pipelines).ToListAsync()
            }
        | _ -> task { return ResizeArray<_>() }

    let private idToGuid (competitionId, email) =
        Predictions.createId (competitionId, Predictions.Email email)

    let delete db id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        let! _ = (getCollection db).DeleteOneAsync(idFilter)
        ()
    }

    let insert db prediction = task {
        let! _ = (getCollection db).InsertOneAsync(prediction)
        ()
    }

    let get db id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), (idToGuid id))
        return! (getCollection db).Find(idFilter) |> FindFluent.trySingleAsync
    }

    let getById db id = task {
        let idFilter = Builders.Filter.Eq((fun x -> x.Id), id)
        return! (getCollection db).Find(idFilter).SingleAsync()
    }

    let find db (competitionId: Guid) (term: string) = task {
        let filter =
            Builders.Filter.And(
                Builders.Filter.Eq((fun x -> x.CompetitionId), competitionId),
                Builders.Filter.Regex(FieldDefinition.op_Implicit "Name", BsonRegularExpression(term, "i"))
            )
        let project =
            ProjectionDefinition<ReadModels.PredictionItem>.op_Implicit """{ _id: 1, Name: 1 }"""
        return! (getCollection db).Find(filter).Project(project).ToListAsync()
    }

    let addToLeague db (predictionId, leagueId) = task {
        let filter = Builders.Filter.Eq((fun x -> x.Id), predictionId)
        let update = Builders.Update.AddToSet(FieldDefinition.op_Implicit "Leagues", leagueId)
        let! _ = (getCollection db).UpdateOneAsync(filter, update)
        ()
    }

    let updateResult db (competitionId, fixtureId: int64, actualResult) = task {
        let! _ =
            (getCollection db).UpdateManyAsync(
                (fun x -> x.CompetitionId = competitionId && x.Fixtures.Any(fun y -> y.FixtureId = fixtureId)),
                Builders.Update.Set((fun x -> x.Fixtures.ElementAt(-1).ActualResult), actualResult)
            )
        ()
    }

    let private getColName = function
        | "GROUP_STAGE" -> "QualifiersRoundOf16"
        | "LAST_16" -> "QualifiersRoundOf8"
        | "QUARTER_FINALS" -> "QualifiersRoundOf4"
        | "SEMI_FINALS" -> "QualifiersRoundOf2"
        | "FINAL" -> "Winner"
        | x -> failwith $"Unexpected value: `%s{x}`"

    let updateQualifiers db (competitionId: Guid, stage: string, teamId: int64, hasQualified: bool) = task {
        let colName = getColName stage
        let! _ =
            (getCollection db).UpdateManyAsync(
                FilterDefinition.op_Implicit $"""{{ CompetitionId: UUID("{competitionId:N}"), "%s{colName}._id": %d{teamId} }}""",
                match colName with
                | "Winner" ->
                    UpdateDefinition.op_Implicit $"""{{ $set: {{ "%s{colName}.HasQualified": %b{hasQualified} }} }}"""
                | _ ->
                    UpdateDefinition.op_Implicit $"""{{ $set: {{ "%s{colName}.$.HasQualified": %b{hasQualified} }} }}"""
            )
        ()
    }

    let setUnqualifiedTeams db (competitionId: Guid, teams: int64 array) = task {
        let teamList = String.Join(",", teams)

        let updateQualifiers name = task {
            let! _ =
                (getCollection db).UpdateManyAsync(
                    FilterDefinition.op_Implicit $"""{{ CompetitionId: UUID("{competitionId:N}") }}""",
                    UpdateDefinition.op_Implicit $"""{{ $set: {{ "%s{name}.$[q].HasQualified": false }} }}""",
                    UpdateOptions(
                        ArrayFilters = [
                            ArrayFilterDefinition<ReadModels.Prediction>.op_Implicit $"""{{ $and: [ {{ "q.HasQualified": {{ $eq: null }} }}, {{ "q._id": {{ $in: [%s{teamList}] }} }} ] }}"""
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
            (getCollection db).UpdateManyAsync(
                FilterDefinition.op_Implicit $"""{{ CompetitionId: UUID("{competitionId:N}"), "Winner.HasQualified": {{ $eq: null }}, "Winner._id": {{ $in: [%s{teamList}] }} }}""",
                UpdateDefinition.op_Implicit """{ $set: { "Winner.HasQualified": false } }"""
            )
        ()
    }

module Leagues =
    let private getCollection (db: IMongoDatabase) =
        db.GetCollection<ReadModels.League>("leagues")

    type Builders = Builders<ReadModels.League>
    type FieldDefinition = FieldDefinition<ReadModels.League>

    let getAll db = task {
        let sort = Builders.Sort.Ascending(FieldDefinition.op_Implicit "Name")
        return! (getCollection db).Find(Builders.Filter.Empty).Sort(sort).ToListAsync()
    }

    let insert db league = task {
        let! _ = (getCollection db).InsertOneAsync(league)
        ()
    }

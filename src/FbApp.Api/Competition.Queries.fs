﻿module FbApp.Competition.Queries

open System
open System.Threading.Tasks
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver


[<BsonIgnoreExtraElements>]
type Competition = {
    Description: string
    ExternalId: int64
    Date: DateTimeOffset
}


type GetActiveCompetition = IMongoDatabase -> Task<Competition option>


let getActiveCompetition: GetActiveCompetition =
    fun db -> task {
        let! competition =
            db.GetCollection<Competition>("competitions")
                .Find(Builders<Competition>.Filter.Eq((fun x -> x.ExternalId), 2000L))
                .Limit(Nullable(1))
                .SingleOrDefaultAsync()
        return (if competition |> box |> isNull then None else Some competition)
    }

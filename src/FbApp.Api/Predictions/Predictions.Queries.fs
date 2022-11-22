module FbApp.Predictions.Queries

open System
open System.Threading.Tasks
open FbApp.Api.Domain
open MongoDB.Bson
open MongoDB.Driver


type GetUserPrediction = IMongoDatabase -> string -> Task<Guid option>


let getUserPrediction: GetUserPrediction =
    fun db email  -> task {
        let competitionId = Competitions.createId 2000L
        let predictionId = Predictions.createId (competitionId, Predictions.Email email)

        let! predictionsCount =
            db.GetCollection<BsonDocument>("predictions")
                .Find(Builders<BsonDocument>.Filter.Eq("_id", predictionId))
                .CountDocumentsAsync()

        return (if predictionsCount = 0 then None else Some predictionId)
    }

module FbApp.Competitions.Queries

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


type GetActiveCompetition = int64 -> IMongoDatabase -> Task<Competition option>


let getActiveCompetition: GetActiveCompetition =
    fun competitionId db -> task {
        let! competition =
            db.GetCollection<Competition>("competitions")
                .Find(Builders<Competition>.Filter.Eq((fun x -> x.ExternalId), competitionId))
                .Limit(Nullable(1))
                .SingleOrDefaultAsync()
        return (if competition |> box |> isNull then None else Some competition)
    }

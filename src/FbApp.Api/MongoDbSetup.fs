module FbApp.Api.MongoDbSetup

#nowarn "44"

open MongoDB.Bson
open MongoDB.Bson.Serialization
open MongoDB.Bson.Serialization.Serializers

let init () =
    BsonDefaults.GuidRepresentationMode <- GuidRepresentationMode.V3
    BsonSerializer.RegisterSerializer(GuidSerializer(GuidRepresentation.Standard))

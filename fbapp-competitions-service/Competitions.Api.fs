module FbApp.Competitions.Api


open Giraffe
open System


type GetActiveCompetitionApi = HttpHandler


let getActiveCompetitionApi: GetActiveCompetitionApi =
    fun next ctx ->
        //let dto: ActiveCompetitionDto =
        //    {
        //    Id = Guid.NewGuid()
        //    Name = "Jalgpalli EM - EURO 2020"
        //    Status = ""
        //    }
        //Successful.OK dto
        RequestErrors.NOT_FOUND "" next ctx

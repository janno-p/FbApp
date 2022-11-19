module FbApp.Competition.Api

open Giraffe
open FbApp.Competition.Dto
open FbApp.Competition.Queries


let getCompetitionStatus : HttpHandler =
    fun next ctx -> task {
        let! competition = getActiveCompetition (ctx.GetService<_>())
        let dto = CompetitionStatusDto.fromCompetition competition
        return! Successful.OK dto next ctx
    }

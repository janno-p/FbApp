module FbApp.Competitions.Api

open FbApp.Api
open Giraffe
open FbApp.Competitions.Dto
open FbApp.Competitions.Queries


let getCompetitionStatus : HttpHandler =
    fun next ctx -> task {
        let! competition = getActiveCompetition FootballData.ActiveCompetition (ctx.GetService<_>())
        let dto = CompetitionStatusDto.fromCompetition competition
        return! Successful.OK dto next ctx
    }

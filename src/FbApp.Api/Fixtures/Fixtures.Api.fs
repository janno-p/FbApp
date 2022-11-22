module FbApp.Fixtures.Api

open FbApp.Fixtures.Dto
open FbApp.Fixtures.Queries
open Giraffe
open System
open Microsoft.AspNetCore.Http


let db (ctx: HttpContext) f =
    f (ctx.GetService<_>())


let getFixture (fixtureId: Guid) : HttpHandler =
    fun next ctx -> task {
        let! fixture = db ctx getFixtureById fixtureId
        match fixture with
        | Some fixture ->
            let dto = FixtureDto.fromFixture fixture
            return! Successful.OK dto next ctx
        | None ->
            return! RequestErrors.NOT_FOUND "Fixture does not exist" next ctx
    }


let getDefaultFixture : HttpHandler =
    fun next ctx -> task {
        let! fixtureId = db ctx getNearestFixtureId
        return! getFixture fixtureId next ctx
    }

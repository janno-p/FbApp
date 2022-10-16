module FbApp.Auth.IntegrationTests.UnitTest1

open FbApp.Auth.IntegrationTests.Testing
open FsUnit
open NUnit.Framework

[<Test>]
let Test1 () = task {
    let! _ = createClient().GetAsync("/")

    Assert.Pass()
}

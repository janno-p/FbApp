namespace FbApp.Api.IntegrationTests

open TUnit.Core
open TUnit.Assertions
open TUnit.Assertions.Extensions
open TUnit.Assertions.FSharp.TaskAssert

type Tests() =
    [<Test>]
    member _.``My test``() = taskAssert {
        let value = true
        do! Assert.That(value).IsTrue()
    }

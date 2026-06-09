namespace FbApp.Auth.IntegrationTests

open FbApp.Auth
open TUnit.AspNetCore

[<AbstractClass>]
type TestBase() =
    inherit WebApplicationTest<AuthApiFactory, Worker>()

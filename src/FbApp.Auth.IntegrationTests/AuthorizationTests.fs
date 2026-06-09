namespace FbApp.Auth.IntegrationTests

open System.Text.Json
open System.Text.Json.Nodes
open TUnit.Assertions.FSharp.TaskAssert
open TUnit.Assertions
open TUnit.Assertions.Extensions
open TUnit.Core

type AuthorizationTests() =
    inherit TestBase()

    [<Test>]
    member this.``returns valid openid configuration`` () = taskAssert {
        let client = this.Factory.CreateClient()

        let! response = client.GetAsync "/.well-known/openid-configuration"

        do! Assert.That(response.StatusCode).IsSuccess()

        let! content = response.Content.ReadAsStringAsync()
        let node = JsonNode.Parse content

        do! Assert.That(node["issuer"].GetValue<string>()).IsEqualTo<string> "http://localhost/"
        do! Assert.That(node["authorization_endpoint"].GetValue<string>()).IsEqualTo<string> "http://localhost/connect/authorize"
        do! Assert.That(node["token_endpoint"].GetValue<string>()).IsEqualTo<string> "http://localhost/connect/token"
        do! Assert.That(node["end_session_endpoint"].GetValue<string>()).IsEqualTo<string> "http://localhost/connect/logout"
        do! Assert.That(node["userinfo_endpoint"].GetValue<string>()).IsEqualTo<string> "http://localhost/connect/userinfo"

        do! Assert.That<string[]>(node["grant_types_supported"].Deserialize<string[]>()).IsEquivalentTo ["authorization_code"; "refresh_token"]
        do! Assert.That<string[]>(node["response_types_supported"].Deserialize<string[]>()).IsEquivalentTo ["code"]
        do! Assert.That<string[]>(node["scopes_supported"].Deserialize<string[]>()).IsEquivalentTo ["openid"; "email"; "profile"; "roles"; "offline_access"]
        do! Assert.That<string[]>(node["claims_supported"].Deserialize<string[]>()).IsEquivalentTo ["aud"; "exp"; "iat"; "iss"; "sub"]
    }

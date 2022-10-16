module FbApp.Auth.IntegrationTests.AuthorizationTests

open System.Text.Json
open System.Text.Json.Nodes
open FbApp.Auth.IntegrationTests.Testing
open FsUnit
open NUnit.Framework

[<Test>]
let ``returns valid openid configuration`` () = task {
    let! response = createClient().GetAsync("/.well-known/openid-configuration")

    response.IsSuccessStatusCode |> should be True

    let! content = response.Content.ReadAsStringAsync()
    let node = JsonNode.Parse(content)

    node["issuer"].GetValue<string>() |> should equal "http://localhost/"
    node["authorization_endpoint"].GetValue<string>() |> should equal "http://localhost/connect/authorize"
    node["token_endpoint"].GetValue<string>() |> should equal "http://localhost/connect/token"
    node["end_session_endpoint"].GetValue<string>() |> should equal "http://localhost/connect/logout"
    node["userinfo_endpoint"].GetValue<string>() |> should equal "http://localhost/connect/userinfo"

    node["grant_types_supported"].Deserialize<string[]>() |> should be (equivalent ["authorization_code"])
    node["response_types_supported"].Deserialize<string[]>() |> should be (equivalent ["code"])
    node["scopes_supported"].Deserialize<string[]>() |> should be (equivalent ["openid"; "email"; "profile"; "roles"])
    node["claims_supported"].Deserialize<string[]>() |> should be (equivalent ["aud"; "exp"; "iat"; "iss"; "sub"])
}

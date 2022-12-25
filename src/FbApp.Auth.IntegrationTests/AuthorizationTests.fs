module FbApp.Auth.IntegrationTests.AuthorizationTests

open System.Text.Json
open System.Text.Json.Nodes
open FsUnit
open Xunit

[<Collection("Api")>]
type AuthorizationTests (fixture: AuthApiFixture) =
    [<Fact>]
    member _.``returns valid openid configuration`` () = task {
        let! response = fixture.Client.GetAsync("/.well-known/openid-configuration")

        response.IsSuccessStatusCode |> should be True

        let! content = response.Content.ReadAsStringAsync()
        let node = JsonNode.Parse(content)

        node["issuer"].GetValue<string>() |> should equal "http://localhost/"
        node["authorization_endpoint"].GetValue<string>() |> should equal "http://localhost/connect/authorize"
        node["token_endpoint"].GetValue<string>() |> should equal "http://localhost/connect/token"
        node["end_session_endpoint"].GetValue<string>() |> should equal "http://localhost/connect/logout"
        node["userinfo_endpoint"].GetValue<string>() |> should equal "http://localhost/connect/userinfo"

        node["grant_types_supported"].Deserialize<string[]>() |> should be (equalSeq ["authorization_code"; "refresh_token"])
        node["response_types_supported"].Deserialize<string[]>() |> should be (equalSeq ["code"])
        node["scopes_supported"].Deserialize<string[]>() |> should be (equalSeq ["openid"; "email"; "profile"; "roles"; "offline_access"])
        node["claims_supported"].Deserialize<string[]>() |> should be (equalSeq ["aud"; "exp"; "iat"; "iss"; "sub"])
    }

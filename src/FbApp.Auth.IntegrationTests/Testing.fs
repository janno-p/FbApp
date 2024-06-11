namespace FbApp.Auth.IntegrationTests

open System
open System.Collections.Generic
open System.Net.Http
open FbApp.Auth
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.Extensions.Configuration
open Testcontainers.PostgreSql
open Xunit

type AuthApiFactory (configuration: IDictionary<_,_>) =
    inherit WebApplicationFactory<Program.Metadata>()

    override _.ConfigureWebHost(builder) =
        base.ConfigureWebHost(builder)

        builder.ConfigureAppConfiguration(
            fun ctx opt ->
                opt.Sources.Clear()
                opt.AddInMemoryCollection(configuration) |> ignore
                ) |> ignore

type AuthApiFixture () =
    let dbContainer =
        PostgreSqlBuilder()
            .WithDatabase("fbapp-auth")
            .WithUsername("postgres")
            .WithPassword("password")
            .Build()

    let mutable factory = Unchecked.defaultof<AuthApiFactory>

    member _.Client with get(): HttpClient = factory.CreateClient()

    interface IAsyncLifetime with
        member _.InitializeAsync() = task {
            do! dbContainer.StartAsync()
            let configuration = dict [
                "ConnectionStrings:postgres", dbContainer.GetConnectionString()
                "Google:Authentication:ClientId", "**id**"
                "Google:Authentication:ClientSecret", "**secret**"
            ]
            factory <- new AuthApiFactory(configuration)
        }

        member _.DisposeAsync() = task {
            do! dbContainer.StopAsync()
        }

    interface IDisposable with
        member _.Dispose() =
            ()

[<CollectionDefinition("Api")>]
type ApiCollectionDefinition () =
    interface ICollectionFixture<AuthApiFixture>

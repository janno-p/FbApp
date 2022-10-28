namespace FbApp.Auth.IntegrationTests

open System.Collections.Generic
open DotNet.Testcontainers.Builders
open DotNet.Testcontainers.Configurations
open DotNet.Testcontainers.Containers
open FbApp.Auth
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.Extensions.Configuration
open NUnit.Framework

type AuthApiFactory (configuration: IDictionary<_,_>) =
    inherit WebApplicationFactory<Program.Metadata>()

    override _.ConfigureWebHost(builder) =
        base.ConfigureWebHost(builder)

        builder.ConfigureAppConfiguration(
            fun ctx opt ->
                opt.Sources.Clear()
                opt.AddInMemoryCollection(configuration) |> ignore
                ) |> ignore

[<SetUpFixture>]
module Testing =
    let private dbContainer =
        let configuration = new PostgreSqlTestcontainerConfiguration(
            Database = "fbapp-auth",
            Username = "postgres",
            Password = "password"
        )
        TestcontainersBuilder<PostgreSqlTestcontainer>()
            .WithDatabase(configuration)
            .Build()

    let mutable private factory = Unchecked.defaultof<AuthApiFactory>

    [<OneTimeSetUp>]
    let ``run before any tests`` () = task {
        do! dbContainer.StartAsync()
        let configuration = dict [
            "ConnectionStrings:postgres", dbContainer.ConnectionString
            "Google:Authentication:ClientId", "**id**"
            "Google:Authentication:ClientSecret", "**secret**"
        ]
        factory <- new AuthApiFactory(configuration)
    }

    [<OneTimeTearDown>]
    let ``run after all tests`` () = task {
        do! dbContainer.StopAsync()
    }

    let createClient() =
        factory.CreateClient()

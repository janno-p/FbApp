namespace FbApp.Auth.IntegrationTests

open System
open System.Threading.Tasks
open Testcontainers.PostgreSql
open TUnit.Core.Interfaces

type AuthApiDatabase() =
    let container =
        PostgreSqlBuilder("postgres:18.3")
            .WithDatabase("fbapp-auth")
            .WithUsername("postgres")
            .WithPassword("password")
            .Build()

    member val Container = container with get

    interface IAsyncInitializer with
        member _.InitializeAsync() = task {
            do! container.StartAsync()
        }

    interface IAsyncDisposable with
        member _.DisposeAsync() = ValueTask(task {
            do! container.DisposeAsync()
        })

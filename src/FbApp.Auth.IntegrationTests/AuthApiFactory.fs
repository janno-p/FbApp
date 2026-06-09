namespace FbApp.Auth.IntegrationTests

open FbApp.Auth
open Microsoft.Extensions.Configuration
open TUnit.AspNetCore
open TUnit.Core

type AuthApiFactory() as this =
    inherit TestWebApplicationFactory<Worker>()

    [<ClassDataSource(typeof<AuthApiDatabase>, Shared = [| SharedType.PerTestSession |])>]
    member val Database = Unchecked.defaultof<AuthApiDatabase> with get, set

    override _.ConfigureWebHost builder = 
        base.ConfigureWebHost builder

        builder.ConfigureAppConfiguration(fun ctx opt ->
            opt.Sources.Clear()

            let configuration = dict [
                "ConnectionStrings:postgres", this.Database.Container.GetConnectionString()
                "Google:Authentication:ClientId", "**id**"
                "Google:Authentication:ClientSecret", "**secret**"
            ]

            opt.AddInMemoryCollection configuration |> ignore
        ) |> ignore

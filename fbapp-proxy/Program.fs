module FbApp.Proxy.Program

open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting

let createHostBuilder args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webBuilder ->
            webBuilder.UseStartup<Startup>() |> ignore
        )

[<EntryPoint>]
let main args =
    createHostBuilder(args).Build().Run()
    0

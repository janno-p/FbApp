module FbApp.Server.Program

open FbApp.Server
open FbApp.Server.Common
open FbApp.Server.HttpsConfig
open Giraffe
open Giraffe.HttpStatusCodeHandlers
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Configuration.UserSecrets
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Saturn
open System.IO

let clientPath = Path.Combine("..", "Client", "dist", "spa-mat") |> Path.GetFullPath

let notLoggedIn = RequestErrors.FORBIDDEN "You must be logged in"

let authPipe = pipeline {
    requires_authentication notLoggedIn
}

let apiRouter = scope {
    post "/tokeninfo" Auth.tokenInfo
    post "/tokensignin" Auth.tokenSignIn
    post "/tokensignout" Auth.tokenSignOut
}

let mainRouter = scope {
    get "/" (Path.Combine(clientPath, "index.html") |> ResponseWriters.htmlFile)
    forward "/api" apiRouter
}

let endpoints = [
    { EndpointConfiguration.Default with
        Port = Some 5000 }
    { EndpointConfiguration.Default with 
        Port = Some 5001
        Scheme = Https
        FilePath = Some (Path.Combine(__SOURCE_DIRECTORY__, "..", "FbApp.pfx")) }
]

let configureServices (context: WebHostBuilderContext) (services: IServiceCollection) =
    services.Configure<AuthOptions>(context.Configuration.GetSection("Authentication")) |> ignore
    services.Configure<GoogleOptions>(context.Configuration.GetSection("Authentication:Google")) |> ignore

let configureAppConfiguration (context: WebHostBuilderContext) (config: IConfigurationBuilder) =
    config.AddJsonFile("appsettings.json", optional=false, reloadOnChange=true)
          .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName, optional=true, reloadOnChange=true)
          .AddEnvironmentVariables()
          .AddUserSecrets<EndpointConfiguration>()
    |> ignore

let app = application {
    router mainRouter
    memory_cache
    use_gzip

    use_cookies_authentication "jnx.era.ee"

    app_config (fun app ->
        app.UseStaticFiles(
            new StaticFileOptions(
                FileProvider = new PhysicalFileProvider(clientPath),
                RequestPath = PathString.Empty
            )
        )
    )

    host_config (fun host ->
        host.UseKestrel(fun o -> o.ConfigureEndpoints endpoints)
            .ConfigureAppConfiguration(configureAppConfiguration)
            .ConfigureServices(configureServices)
    )
}

run app

[<assembly: UserSecretsIdAttribute("d6072641-6e1a-4bbc-bbb6-d355f0e38db4")>]
do()

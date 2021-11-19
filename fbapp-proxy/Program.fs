module FbApp.Proxy.Program


open Giraffe
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Saturn
open System
open System.Collections.Generic
open System.Threading.Tasks
open Yarp.ReverseProxy.Configuration


// https://github.com/dotnet/fsharp/pull/11552
let initProp (target: obj) (propertyName: string) (value: obj) =
    let targetType = target.GetType()
    match targetType.GetProperty(propertyName) |> Option.ofObj |> Option.bind (fun x -> x.GetSetMethod() |> Option.ofObj) with
    | None -> failwith $"Could not find property %s{propertyName} setter for type %s{targetType.Name}"
    | Some prop -> prop.Invoke(target, [| value |]) |> ignore


type DaprConfigFilter() =
    let [<Literal>] DaprPrefix = "dapr:"

    interface IProxyConfigFilter with
        member _.ConfigureClusterAsync(originalCluster, _) =
            let newDestinations = Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)

            let daprPort =
                match Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") |> Int32.TryParse with
                | true, port -> port
                | false, _ -> 3500

            originalCluster.Destinations
            |> Seq.iter (fun kvp ->
                if kvp.Value.Address.StartsWith(DaprPrefix) then
                    let destination = kvp.Value.``<Clone>$``()
                    $"http://localhost:%d{daprPort}%s{kvp.Value.Address.Substring(DaprPrefix.Length)}" |> initProp destination (nameof destination.Address)
                    newDestinations.Add(kvp.Key, destination)
                else
                    newDestinations.Add(kvp.Key, kvp.Value)
            )

            let cluster = originalCluster.``<Clone>$``()
            newDestinations |> initProp cluster (nameof cluster.Destinations)

            ValueTask<ClusterConfig>(cluster)

        member _.ConfigureRouteAsync(originalRoute, _, _) =
            ValueTask<RouteConfig>(originalRoute)


let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
    let configuration = context.Configuration

    services.AddAuthorization()
    |> ignore

    services.AddReverseProxy()
        .LoadFromConfig(configuration.GetSection("ReverseProxy"))
        .AddConfigFilter<DaprConfigFilter>()
    |> ignore


let routes = router {
    get "/dapr/config" (obj() |> Successful.OK)
}


let configureApplication (app: IApplicationBuilder) =
    let env = Environment.getWebHostEnvironment app

    if env.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseRouting() |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore

    app.UseGiraffe(routes)

    app.UseEndpoints(fun endpoints ->
        endpoints.MapReverseProxy() |> ignore
    ) |> ignore

    app


let configureJwtAuthentication (options: JwtBearerOptions) =
    // configuration.Bind("JwtBearer", options);
    options.Authority <- "http://localhost:7001"
    options.TokenValidationParameters.ValidateAudience <- false
    options.TokenValidationParameters.ValidIssuer <- "https://localhost:8090/"
    options.RequireHttpsMetadata <- false
    options.SaveToken <- true
    options.Events <-
        JwtBearerEvents(
            OnAuthenticationFailed = (fun context ->
                context.NoResult()
                context.Response.StatusCode <- StatusCodes.Status500InternalServerError
                context.Response.ContentType <- "text/plain"
                context.Response.WriteAsync(context.Exception.ToString())
            )
        )
    options.Validate()


let app = application {
    no_router
    app_config configureApplication
    host_config (fun host -> host.ConfigureServices(configureServices))
    use_jwt_authentication_with_config configureJwtAuthentication
}


run app

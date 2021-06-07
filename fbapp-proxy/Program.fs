module FbApp.Proxy.Program


open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Http
open Saturn
open Microsoft.AspNetCore.Authentication.JwtBearer


let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
    let configuration = context.Configuration

    services.AddAuthorization() |> ignore
    services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy")) |> ignore


let configureApplication (app: IApplicationBuilder) =
    let env = Environment.getWebHostEnvironment app

    if env.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseRouting() |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    
    app.UseEndpoints(fun endpoints ->
        endpoints.MapReverseProxy() |> ignore
    ) |> ignore

    app


let configureJwtAuthentication (options: JwtBearerOptions) =
    options.Authority <- "http://localhost:7002"
    options.Audience <- "fbapp-ui-client"
    options.RequireHttpsMetadata <- false
    options.SaveToken <- true

    options.Events <-
        JwtBearerEvents(
            OnAuthenticationFailed = fun context ->
                context.NoResult()
                context.Response.StatusCode <- StatusCodes.Status500InternalServerError
                context.Response.ContentType <- "text/plain";
                context.Response.WriteAsync(context.Exception.ToString())
        )

    options.Validate()


let app = application {
    app_config configureApplication
    host_config (fun host -> host.ConfigureServices(configureServices))
    use_jwt_authentication_with_config configureJwtAuthentication
}


run app

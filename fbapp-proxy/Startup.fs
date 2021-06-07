namespace FbApp.Proxy

open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Http
open System.Threading.Tasks

type Startup(configuration: IConfiguration) =

    member _.ConfigureServices(services: IServiceCollection) =
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, fun options ->
                options.Events.OnRedirectToLogin <- fun ctx ->
                    ctx.Response.StatusCode <- StatusCodes.Status401Unauthorized
                    Task.CompletedTask)
        |> ignore

        services.AddAuthorization() |> ignore
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy")) |> ignore

    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseRouting()
           .UseAuthentication()
           .UseAuthorization()
           .UseEndpoints(fun endpoints -> endpoints.MapReverseProxy() |> ignore)
        |> ignore

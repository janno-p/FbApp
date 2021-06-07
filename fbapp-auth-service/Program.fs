module FbApp.Auth.Program


open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Identity
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open OpenIddict.Abstractions
open OpenIddict.Server.AspNetCore
open Quartz
open Saturn
open System
open System.Collections.Generic


let toListAsync (source: IAsyncEnumerable<_>) = task {
    let list = ResizeArray<_>()
    let e = source.GetAsyncEnumerator()
    let rec iter () = unitTask {
        match! e.MoveNextAsync() with
        | true ->
            list.Add(e.Current)
            do! iter()
        | false -> ()
    }
    do! iter()
    return list
}


type ApplicationUser() =
    inherit IdentityUser()


let configureServices (services: IServiceCollection) =
    services.AddAuthorization() |> ignore

    services.AddDbContext<ApplicationDbContext>(fun sp options ->
        let configuration = sp.GetRequiredService<IConfiguration>()
        options.UseNpgsql(configuration.GetConnectionString("Default")) |> ignore
        options.UseOpenIddict<Guid>() |> ignore
    ) |> ignore

    services
        .AddAuthentication(fun options ->
            options.DefaultScheme <- IdentityConstants.ApplicationScheme
            options.DefaultSignInScheme <- IdentityConstants.ExternalScheme)
        .AddIdentityCookies()
    |> ignore

    services
        .AddIdentityCore<ApplicationUser>(fun options ->
            options.Stores.MaxLengthForKeys <- 128
            options.SignIn.RequireConfirmedAccount <- true)
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders()
        .AddSignInManager()
    |> ignore

    services.Configure<IdentityOptions>(fun (options: IdentityOptions) ->
        options.ClaimsIdentity.UserNameClaimType <- OpenIddictConstants.Claims.Name
        options.ClaimsIdentity.UserIdClaimType <- OpenIddictConstants.Claims.Subject
        options.ClaimsIdentity.RoleClaimType <- OpenIddictConstants.Claims.Role
    ) |> ignore

    services.AddQuartz(fun options ->
        options.UseMicrosoftDependencyInjectionJobFactory()
        options.UseSimpleTypeLoader()
        options.UseInMemoryStore()
    ) |> ignore

    services.AddQuartzHostedService(fun options ->
        options.WaitForJobsToComplete <- true
    ) |> ignore

    services.AddOpenIddict()
        .AddCore(fun options ->
            options.UseEntityFrameworkCore()
                .UseDbContext<ApplicationDbContext>()
                .ReplaceDefaultEntities<Guid>()
            |> ignore
            options.UseQuartz() |> ignore)
        .AddServer(fun options ->
            options.SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
            |> ignore
            options.RegisterScopes(OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Roles)
            |> ignore
            options.AllowAuthorizationCodeFlow()
            |> ignore
            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate()
            |> ignore
            options.RemoveEventHandler(OpenIddictServerAspNetCoreHandlers.ValidateTransportSecurityRequirement.Descriptor)
            |> ignore
            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
            |> ignore)
        .AddValidation(fun options ->
            options.UseLocalServer() |> ignore
            options.UseAspNetCore() |> ignore)
    |> ignore

    services.AddHostedService<Worker>() |> ignore

    services


let configureApplication (app: IApplicationBuilder) =
    let env = Environment.getWebHostEnvironment app

    if env.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    // app.UseRouting() |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore

    //app.UseEndpoints(fun endpoints ->
    //    endpoints.MapControllers() |> ignore
    //    endpoints.MapDefaultControllerRoute() |> ignore
    //) |> ignore

    //app.UseWelcomePage() |> ignore

    app


let authorize: HttpHandler =
    fun next ctx -> task {
        let request = ctx.GetOpenIddictServerRequest()
        if request = null then
            failwith "The OpenID Connect request cannot be retrieved"

        match! ctx.AuthenticateAsync(IdentityConstants.ApplicationScheme) with
        | null as v | v when not v.Succeeded ->
            do! ctx.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, AuthenticationProperties(dict [
                (OpenIddictServerAspNetCoreConstants.Properties.Error, OpenIddictConstants.Errors.LoginRequired)
                (OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription, "The user is not logged in")
            ]))
            return Some ctx
        | result ->
            let properties = result.Properties |> Option.ofObj
            let issuedUtc = properties |> Option.bind (fun p -> p.IssuedUtc |> Option.ofNullable) |> Option.defaultValue DateTimeOffset.MinValue
            match request.MaxAge |> Option.ofNullable with
            | Some maxAge when DateTimeOffset.UtcNow - issuedUtc > TimeSpan.FromSeconds(Convert.ToDouble(maxAge)) ->
                do! ctx.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, AuthenticationProperties(dict [
                    (OpenIddictServerAspNetCoreConstants.Properties.Error, OpenIddictConstants.Errors.LoginRequired)
                    (OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription, "The user is not logged in")
                ]))
                return Some ctx
            | _ ->
                let userManager = ctx.RequestServices.GetRequiredService<UserManager<_>>()
                let! user = userManager.GetUserAsync(result.Principal)
                if user = null then
                    failwith "The user details cannot be retrieved"

                let applicationManager = ctx.RequestServices.GetRequiredService<IOpenIddictApplicationManager>()
                let! application = applicationManager.FindByClientIdAsync(request.ClientId)
                if application = null then
                    failwith "Details concerning the calling client application cannot be found"

                let! subject = userManager.GetUserIdAsync(user)
                let! client = applicationManager.GetIdAsync(application)

                let authorizationManager = ctx.RequestServices.GetRequiredService<IOpenIddictAuthorizationManager>()
                let! authorizations =
                    authorizationManager.FindAsync(
                        subject,
                        client,
                        OpenIddictConstants.Statuses.Valid,
                        OpenIddictConstants.AuthorizationTypes.Permanent,
                        request.GetScopes()
                    ) |> toListAsync

                let signInManager = ctx.RequestServices.GetRequiredService<SignInManager<_>>()
                let! principal = signInManager.CreateUserPrincipalAsync(user)

                principal.SetScopes(request.GetScopes()) |> ignore

                let scopeManager = ctx.RequestServices.GetRequiredService<IOpenIddictScopeManager>()
                let! resources = scopeManager.ListResourcesAsync(principal.GetScopes()) |> toListAsync
                principal.SetResources(resources) |> ignore

                let! authorization =
                    match authorizations |> Seq.tryLast with
                    | Some x ->
                        System.Threading.Tasks.ValueTask.FromResult(x)
                    | None ->
                        authorizationManager.CreateAsync(
                            principal,
                            subject,
                            client,
                            OpenIddictConstants.AuthorizationTypes.Permanent,
                            principal.GetScopes()
                        )
                let! authorizationId = authorizationManager.GetIdAsync(authorization)
                principal.SetAuthorizationId(authorizationId) |> ignore

                let getDestinations (claim: Security.Claims.Claim) = seq {
                    match claim.Type with
                    | OpenIddictConstants.Claims.Name ->
                        yield OpenIddictConstants.Destinations.AccessToken
                        if principal.HasScope(OpenIddictConstants.Scopes.Profile) then
                            yield OpenIddictConstants.Destinations.IdentityToken
                    | OpenIddictConstants.Claims.Email ->
                        yield OpenIddictConstants.Destinations.AccessToken
                        if principal.HasScope(OpenIddictConstants.Scopes.Email) then
                            yield OpenIddictConstants.Destinations.IdentityToken
                    | OpenIddictConstants.Claims.Role ->
                        yield OpenIddictConstants.Destinations.AccessToken
                        if principal.HasScope(OpenIddictConstants.Scopes.Roles) then
                            yield OpenIddictConstants.Destinations.IdentityToken
                    | "AspNet.Identity.SecurityStamp" ->
                        ()
                    | _ ->
                        yield OpenIddictConstants.Destinations.AccessToken
                }

                principal.Claims |> Seq.iter (fun claim -> claim.SetDestinations(getDestinations claim) |> ignore)

                do! ctx.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal)

                return! Successful.OK "" next ctx
    }


let routes = router {
    get "/connect/authorize" authorize
}


let app = application {
    app_config configureApplication
    service_config configureServices
    use_router routes
}


run app

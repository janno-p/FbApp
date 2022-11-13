module FbApp.Auth.Program


open FSharp.Control
open Giraffe
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Google
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.Identity
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open OpenIddict.Abstractions
open OpenIddict.Server.AspNetCore
open Quartz
open Saturn
open System
open System.Collections.Generic
open System.Security.Claims


module Ioc =
    let get<'t> (ctx: HttpContext) =
        let scoped = lazy ctx.RequestServices.GetRequiredService<'t>()
        (fun () -> scoped.Value)

    let create ctx =
        {|
            UserManager = ctx |> get<UserManager<ApplicationUser>>
            SignInManager = ctx |> get<SignInManager<ApplicationUser>>
            OpenIddictApplicationManager = ctx |> get<IOpenIddictApplicationManager>
            OpenIddictAuthorizationManager = ctx |> get<IOpenIddictAuthorizationManager>
            OpenIddictScopeManager = ctx |> get<IOpenIddictScopeManager>
            HttpClientFactory = ctx |> get<System.Net.Http.IHttpClientFactory>
            Configuration = ctx |> get<IConfiguration>
        |}

let getDestinations (principal: ClaimsPrincipal) (claim: Claim) = seq {
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
    | OpenIddictConstants.Claims.Picture ->
        if principal.HasScope(OpenIddictConstants.Scopes.Profile) then
            yield OpenIddictConstants.Destinations.IdentityToken
    | "AspNet.Identity.SecurityStamp" ->
        ()
    | _ ->
        yield OpenIddictConstants.Destinations.AccessToken
}


let authorize: HttpHandler =
    fun _ ctx -> task {
        let svc = ctx |> Ioc.create

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
                let! user = svc.UserManager().GetUserAsync(result.Principal)
                if user = null then
                    failwith "The user details cannot be retrieved"

                let! application = svc.OpenIddictApplicationManager().FindByClientIdAsync(request.ClientId)
                if application = null then
                    failwith "Details concerning the calling client application cannot be found"

                let! subject = svc.UserManager().GetUserIdAsync(user)
                let! client = svc.OpenIddictApplicationManager().GetIdAsync(application)

                let! authorizations =
                    svc.OpenIddictAuthorizationManager().FindAsync(
                        subject,
                        client,
                        OpenIddictConstants.Statuses.Valid,
                        OpenIddictConstants.AuthorizationTypes.Permanent,
                        request.GetScopes()
                    ) |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toListAsync

                let! principal = svc.SignInManager().CreateUserPrincipalAsync(user)

                principal.SetScopes(request.GetScopes()) |> ignore

                let! resources = svc.OpenIddictScopeManager().ListResourcesAsync(principal.GetScopes()) |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toListAsync
                principal.SetResources(resources) |> ignore

                let! authorization =
                    match authorizations |> Seq.tryLast with
                    | Some x ->
                        System.Threading.Tasks.ValueTask.FromResult(x)
                    | None ->
                        svc.OpenIddictAuthorizationManager().CreateAsync(
                            principal,
                            subject,
                            client,
                            OpenIddictConstants.AuthorizationTypes.Permanent,
                            principal.GetScopes()
                        )
                let! authorizationId = svc.OpenIddictAuthorizationManager().GetIdAsync(authorization)
                principal.SetAuthorizationId(authorizationId) |> ignore

                let getDestinations = getDestinations principal

                principal.Claims |> Seq.iter (fun claim -> claim.SetDestinations(getDestinations claim) |> ignore)

                do! ctx.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal)

                return Some ctx
    }


let exchangeToken: HttpHandler =
    fun next ctx -> task {
        let svc = ctx |> Ioc.create

        match ctx.GetOpenIddictServerRequest() with
        | null ->
            return! RequestErrors.BAD_REQUEST "The OpenID Connect request cannot be retrieved" next ctx
        | request when request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType() ->
            let! authenticateResult = ctx.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            let principal = authenticateResult.Principal

            match! svc.UserManager().GetUserAsync(principal) with
            | null ->
                do! ctx.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, AuthenticationProperties(dict [
                    (OpenIddictServerAspNetCoreConstants.Properties.Error, OpenIddictConstants.Errors.InvalidGrant)
                    (OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription, "The token is no longer valid")
                ]))
                return Some ctx
            | user ->
                let! canSignIn = svc.SignInManager().CanSignInAsync(user)
                if canSignIn then
                    let arr = principal.Claims |> Seq.map (fun x -> $"%s{x.Type}=%s{x.Value}")
                    ctx.GetLogger("YYY").LogInformation(": " + ctx.GetJsonSerializer().SerializeToString(arr))
                    principal.SetClaim(OpenIddictConstants.Claims.Name, user.FullName) |> ignore
                    principal.SetClaim(OpenIddictConstants.Claims.Email, user.Email) |> ignore
                    principal.Claims |> Seq.iter (fun claim -> claim.SetDestinations(getDestinations principal claim) |> ignore)
                    do! ctx.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal)
                    return Some ctx
                else
                    do! ctx.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, AuthenticationProperties(dict [
                        (OpenIddictServerAspNetCoreConstants.Properties.Error, OpenIddictConstants.Errors.InvalidGrant)
                        (OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription, "The user is no longer allowed to sign in")
                    ]))
                    return Some ctx
        | request ->
            return! RequestErrors.BAD_REQUEST $"The specified grant type '%s{request.GrantType}' is not supported" next ctx
    }


let googleLogin: HttpHandler =
    fun _ ctx -> task {
        let svc = ctx |> Ioc.create
        let redirectUrl = $"%s{ctx.Request.Scheme}://%s{ctx.Request.Host.Value}/connect/google/complete"
        let properties = svc.SignInManager().ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl)
        do! ctx.ChallengeAsync(GoogleDefaults.AuthenticationScheme, properties)
        return Some ctx
    }


[<AllowNullLiteral>]
type Source () =
    member val ``type`` = Unchecked.defaultof<string> with get, set
    member val id = Unchecked.defaultof<string> with get, set


[<AllowNullLiteral>]
type Metadata () =
    member val primary = false with get, set
    member val source = Unchecked.defaultof<Source> with get, set


[<AllowNullLiteral>]
type Photo () =
    member val metadata = Unchecked.defaultof<Metadata> with get, set
    member val url = Unchecked.defaultof<string> with get, set


[<AllowNullLiteral>]
type PeopleApiPhotos () =
    member val resourceName = Unchecked.defaultof<string> with get, set
    member val etag = Unchecked.defaultof<string> with get, set
    member val photos = ResizeArray<Photo>() with get, set


let updateUser (user: ApplicationUser) (principal: ClaimsPrincipal) =
    user.Email <- principal.FindFirstValue(ClaimTypes.Email)
    user.EmailConfirmed <- true
    user.FullName <- principal.FindFirstValue(ClaimTypes.Name)
    user.GivenName <- principal.FindFirstValue(ClaimTypes.GivenName)
    user.PictureUrl <- principal.FindFirstValue(OpenIddictConstants.Claims.Picture)
    user.Provider <- "Google"
    user.ProviderId <- principal.FindFirstValue(ClaimTypes.NameIdentifier)
    user.Surname <- principal.FindFirstValue(ClaimTypes.Surname)
    user.UserName <- principal.FindFirstValue(ClaimTypes.Email)


let updateAdminRole (user: ApplicationUser) (principal: ClaimsPrincipal) (ctx: HttpContext) = task {
    let svc = ctx |> Ioc.create
    let userEmail = principal.FindFirstValue(ClaimTypes.Email)
    let isDefaultAdmin = userEmail.Equals(svc.Configuration()["Authorization:DefaultAdmin"])
    let! hasAdminRole = svc.UserManager().IsInRoleAsync(user, "admin")
    if isDefaultAdmin <> hasAdminRole then
        let action = if hasAdminRole then svc.UserManager().RemoveFromRoleAsync else svc.UserManager().AddToRoleAsync
        let! _ = action(user, "admin")
        ()
}


let googleResponse: HttpHandler =
    fun next ctx -> task {
        let svc = ctx |> Ioc.create

        match! svc.SignInManager().GetExternalLoginInfoAsync() with
        | null ->
            return! googleLogin next ctx
        | info ->
            let! result = svc.SignInManager().ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false)
            if result.Succeeded then
                let! user = svc.UserManager().FindByNameAsync(info.Principal.FindFirstValue(ClaimTypes.Email))
                updateUser user info.Principal
                do! ctx |> updateAdminRole user info.Principal
                let! _ = svc.UserManager().UpdateAsync(user)
                ()
            else
                let user = ApplicationUser()
                updateUser user info.Principal

                let! identityResult = svc.UserManager().CreateAsync(user)
                if identityResult.Succeeded then
                    let! _ = svc.UserManager().UpdateAsync(user)
                    do! ctx |> updateAdminRole user info.Principal
                    let! identityResult = svc.UserManager().AddLoginAsync(user, info)
                    if identityResult.Succeeded then
                        let! _ = svc.SignInManager().SignInAsync(user, false)
                        ()

            return! redirectTo false "/" next ctx
    }


let userinfo: HttpHandler =
    fun next ctx -> task {
        let svc = ctx |> Ioc.create

        let! authenticateResult = ctx.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
        let principal = authenticateResult.Principal

        match! svc.UserManager().GetUserAsync(principal) with
        | null ->
            return! RequestErrors.FORBIDDEN OpenIddictConstants.Errors.InvalidToken next ctx
        | user ->
            let claims = Dictionary<string, obj>(StringComparer.Ordinal)

            let! subject = svc.UserManager().GetUserIdAsync(user)
            claims[OpenIddictConstants.Claims.Subject] <- box subject

            claims[OpenIddictConstants.Claims.Name] <- box user.FullName
            claims[OpenIddictConstants.Claims.GivenName] <- box user.GivenName
            claims[OpenIddictConstants.Claims.FamilyName] <- box user.Surname
            claims[OpenIddictConstants.Claims.Picture] <- box user.PictureUrl

            if principal.HasScope(OpenIddictConstants.Scopes.Email) then
                let! email = svc.UserManager().GetEmailAsync(user)
                claims[OpenIddictConstants.Claims.Email] <- box email

                let! emailVerified = svc.UserManager().IsEmailConfirmedAsync(user)
                claims[OpenIddictConstants.Claims.EmailVerified] <- box emailVerified

            if principal.HasScope(OpenIddictConstants.Scopes.Phone) then
                let! phoneNumber = svc.UserManager().GetPhoneNumberAsync(user)
                claims[OpenIddictConstants.Claims.PhoneNumber] <- box phoneNumber

                let! phoneNumberVerified = svc.UserManager().IsPhoneNumberConfirmedAsync(user)
                claims[OpenIddictConstants.Claims.PhoneNumberVerified] <- box phoneNumberVerified

            if principal.HasScope(OpenIddictConstants.Scopes.Roles) then
                let! roles = svc.UserManager().GetRolesAsync(user)
                claims[OpenIddictConstants.Claims.Role] <- box roles

            return! Successful.OK claims next ctx
    }


let logout: HttpHandler =
    fun _ ctx -> task {
        let svc = ctx |> Ioc.create
        do! svc.SignInManager().SignOutAsync()
        do! ctx.SignOutAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, AuthenticationProperties(RedirectUri = "/"))
        return Some ctx
    }


module Routes =
    let [<Literal>] Authorize = "/connect/authorize"
    let [<Literal>] Google = "/connect/google"
    let [<Literal>] GoogleComplete = "/connect/google/complete"
    let [<Literal>] Logout = "/connect/logout"
    let [<Literal>] Token = "/connect/token"
    let [<Literal>] Userinfo = "/connect/userinfo"


let routes = router {
    get Routes.Authorize authorize
    get Routes.Google googleLogin
    get Routes.GoogleComplete googleResponse
    get Routes.Logout logout
    post Routes.Token exchangeToken
    get Routes.Userinfo userinfo
}


let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
    services.AddAuthorization() |> ignore

    services.AddDbContext<ApplicationDbContext>(fun options ->
        let connectionString = context.Configuration.GetConnectionString("postgres")
        options.UseNpgsql(connectionString) |> ignore
        options.UseOpenIddict<Guid>() |> ignore
    ) |> ignore

    services.AddAuthentication()
        .AddGoogle(fun options ->
            let googleSection = context.Configuration.GetSection("Google:Authentication")
            options.ClientId <- googleSection["ClientId"]
            options.ClientSecret <- googleSection["ClientSecret"]
            options.CallbackPath <- PathString "/connect/google/callback"
            options.SignInScheme <- IdentityConstants.ExternalScheme
            options.Scope.Add("profile")
            options.ClaimActions.MapJsonKey(OpenIddictConstants.Claims.Picture, "picture"))
    |> ignore

    services.AddIdentity<ApplicationUser, ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders()
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
            options.SetAuthorizationEndpointUris(Routes.Authorize)
                .SetTokenEndpointUris(Routes.Token)
                .SetUserinfoEndpointUris(Routes.Userinfo)
                .SetLogoutEndpointUris(Routes.Logout)
            |> ignore

            options.RegisterScopes(OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Roles) |> ignore
            options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange() |> ignore
            options.AllowRefreshTokenFlow() |> ignore
            options.AddDevelopmentEncryptionCertificate() |> ignore
            options.AddDevelopmentSigningCertificate() |> ignore
            options.RemoveEventHandler(OpenIddictServerAspNetCoreHandlers.ValidateTransportSecurityRequirement.Descriptor) |> ignore
            options.DisableAccessTokenEncryption() |> ignore

            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5)) |> ignore
            options.SetRefreshTokenLifetime(TimeSpan.FromMinutes(30)) |> ignore

            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .EnableUserinfoEndpointPassthrough()
                .EnableLogoutEndpointPassthrough()
            |> ignore)
        .AddValidation(fun options ->
            options.UseLocalServer() |> ignore
            options.UseAspNetCore() |> ignore)
    |> ignore

    services.AddHttpClient() |> ignore

    services.AddHostedService<Worker>() |> ignore


let configureApplication (app: IApplicationBuilder) =
    app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedHost ||| ForwardedHeaders.XForwardedProto))) |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    app


let configureAppConfiguration (context: HostBuilderContext) (config: IConfigurationBuilder) =
    config.AddJsonFile("appsettings.json", optional=true, reloadOnChange=true)
          .AddJsonFile($"appsettings.%s{context.HostingEnvironment.EnvironmentName}.json", optional=true, reloadOnChange=true)
          .AddJsonFile("appsettings.user.json", optional=true, reloadOnChange=true)
          .AddEnvironmentVariables()
    |> ignore


let app = application {
    app_config configureApplication

    host_config (fun host ->
        host.ConfigureServices(configureServices)
            .ConfigureAppConfiguration(configureAppConfiguration))

    use_router routes
}


run app

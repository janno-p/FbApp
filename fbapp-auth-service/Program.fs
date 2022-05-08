module FbApp.Auth.Program


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
open OpenIddict.Abstractions
open OpenIddict.Server.AspNetCore
open Quartz
open Saturn
open System
open System.Collections.Generic
open System.Net.Http.Json
open System.Security.Claims


module Ioc =
    let private get (ctx: HttpContext) = ctx.RequestServices.GetRequiredService<_>()
    type private H<'a> = HttpContext -> 'a
    let userManager: H<UserManager<ApplicationUser>> = get
    let signInManager: H<SignInManager<ApplicationUser>> = get
    let openIddictApplicationManager: H<IOpenIddictApplicationManager> = get
    let openIddictAuthorizationManager: H<IOpenIddictAuthorizationManager> = get
    let openIddictScopeManager: H<IOpenIddictScopeManager> = get
    let httpClientFactory: H<System.Net.Http.IHttpClientFactory> = get
    let httpClient: H<System.Net.Http.HttpClient> = (fun ctx -> (ctx |> httpClientFactory).CreateClient(""))
    let configuration: H<IConfiguration> = get


let toListAsync (source: IAsyncEnumerable<_>) = task {
    let list = ResizeArray<_>()
    let e = source.GetAsyncEnumerator()
    let rec iter () = task {
        match! e.MoveNextAsync() with
        | true ->
            list.Add(e.Current)
            do! iter()
        | false -> ()
    }
    do! iter()
    return list
}


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
                let userManager = ctx |> Ioc.userManager
                let! user = userManager.GetUserAsync(result.Principal)
                if user = null then
                    failwith "The user details cannot be retrieved"

                let applicationManager = ctx |> Ioc.openIddictApplicationManager
                let! application = applicationManager.FindByClientIdAsync(request.ClientId)
                if application = null then
                    failwith "Details concerning the calling client application cannot be found"

                let! subject = userManager.GetUserIdAsync(user)
                let! client = applicationManager.GetIdAsync(application)

                let authorizationManager = ctx |> Ioc.openIddictAuthorizationManager
                let! authorizations =
                    authorizationManager.FindAsync(
                        subject,
                        client,
                        OpenIddictConstants.Statuses.Valid,
                        OpenIddictConstants.AuthorizationTypes.Permanent,
                        request.GetScopes()
                    ) |> toListAsync

                let signInManager = ctx |> Ioc.signInManager
                let! principal = signInManager.CreateUserPrincipalAsync(user)

                principal.SetScopes(request.GetScopes()) |> ignore

                let scopeManager = ctx |> Ioc.openIddictScopeManager
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

                let getDestinations = getDestinations principal

                principal.Claims |> Seq.iter (fun claim -> claim.SetDestinations(getDestinations claim) |> ignore)

                do! ctx.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal)

                return Some ctx
    }


let exchangeToken: HttpHandler =
    fun next ctx -> task {
        match ctx.GetOpenIddictServerRequest() with
        | null ->
            return! RequestErrors.BAD_REQUEST "The OpenID Connect request cannot be retrieved" next ctx
        | request ->
            match request.GrantType with
            | OpenIddictConstants.GrantTypes.AuthorizationCode ->
                let! authenticateResult = ctx.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
                let principal = authenticateResult.Principal

                let userManager = ctx |> Ioc.userManager
                match! userManager.GetUserAsync(principal) with
                | null ->
                    do! ctx.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, AuthenticationProperties(dict [
                        (OpenIddictServerAspNetCoreConstants.Properties.Error, OpenIddictConstants.Errors.InvalidGrant)
                        (OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription, "The token is no longer valid")
                    ]))
                    return Some ctx
                | user ->
                    let signInManager = ctx |> Ioc.signInManager
                    match! signInManager.CanSignInAsync(user) with
                    | false ->
                        do! ctx.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, AuthenticationProperties(dict [
                            (OpenIddictServerAspNetCoreConstants.Properties.Error, OpenIddictConstants.Errors.InvalidGrant)
                            (OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription, "The user is no longer allowed to sign in")
                        ]))
                        return Some ctx
                    | true ->
                        let getDestinations = getDestinations principal
                        principal.Claims |> Seq.iter (fun claim -> claim.SetDestinations(getDestinations claim) |> ignore)
                        do! ctx.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal)
                        return Some ctx
            | grantType ->
                return! RequestErrors.BAD_REQUEST $"The specified grant type '%s{grantType}' is not supported" next ctx
    }


let googleLogin: HttpHandler =
    fun _ ctx -> task {
        let signInManager = ctx |> Ioc.signInManager
        let redirectUrl = $"%s{ctx.Request.Scheme}://%s{ctx.Request.Host.Value}/connect/google/complete"
        let properties = signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl)
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


let getPictureUrl (googleAccountId: string) (ctx: HttpContext) = task {
    let httpClient = ctx |> Ioc.httpClient
    let configuration = ctx |> Ioc.configuration
    let googleApiKey = configuration["Google:ApiKey"]
    let! response = httpClient.GetFromJsonAsync<PeopleApiPhotos>($"https://people.googleapis.com/v1/people/%s{googleAccountId}?personFields=photos&key=%s{googleApiKey}")
    return
        match response with
        | null -> null
        | _ -> response.photos |> Seq.tryHead |> Option.map (fun p -> p.url) |> Option.toObj
}


let googleResponse: HttpHandler =
    fun next ctx -> task {
        let signInManager = ctx |> Ioc.signInManager
        let userManager = ctx |> Ioc.userManager

        match! signInManager.GetExternalLoginInfoAsync() with
        | null ->
            return! googleLogin next ctx
        | info ->
            let! result = signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false)
            let! pictureUrl = ctx |> getPictureUrl (info.Principal.FindFirstValue(ClaimTypes.NameIdentifier))
            if result.Succeeded then
                let! user = userManager.FindByNameAsync(info.Principal.FindFirstValue(ClaimTypes.Email))
                user.PictureUrl <- pictureUrl
                let! _ = userManager.UpdateAsync(user)
                ()
            else
                let user = ApplicationUser()
                user.Email <- info.Principal.FindFirst(ClaimTypes.Email).Value
                user.UserName <- info.Principal.FindFirst(ClaimTypes.Email).Value
                user.PictureUrl <- pictureUrl

                let! identityResult = userManager.CreateAsync(user)
                if identityResult.Succeeded then
                    let! _ = userManager.UpdateAsync(user)
                    let! identityResult = userManager.AddLoginAsync(user, info)
                    if identityResult.Succeeded then
                        let! _ = signInManager.SignInAsync(user, false)
                        () 

            return! redirectTo false "/" next ctx 
    }


// [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
let userinfo: HttpHandler =
    fun next ctx -> task {
        let userManager = Ioc.userManager ctx
        match! userManager.GetUserAsync(ctx.User) with
        | null ->
            return! RequestErrors.FORBIDDEN OpenIddictConstants.Errors.InvalidToken next ctx
        | user ->
            let claims = Dictionary<string, obj>(StringComparer.Ordinal)

            let! subject = userManager.GetUserIdAsync(user)
            claims.[OpenIddictConstants.Claims.Subject] <- box subject
            claims.[OpenIddictConstants.Claims.Picture] <- box user.PictureUrl

            if ctx.User.HasScope(OpenIddictConstants.Scopes.Email) then
                let! email = userManager.GetEmailAsync(user)
                claims.[OpenIddictConstants.Claims.Email] <- box email

                let! emailVerified = userManager.IsEmailConfirmedAsync(user)
                claims.[OpenIddictConstants.Claims.EmailVerified] <- box emailVerified

            if ctx.User.HasScope(OpenIddictConstants.Scopes.Phone) then
                let! phoneNumber = userManager.GetPhoneNumberAsync(user)
                claims.[OpenIddictConstants.Claims.PhoneNumber] <- box phoneNumber

                let! phoneNumberVerified = userManager.IsPhoneNumberConfirmedAsync(user)
                claims.[OpenIddictConstants.Claims.PhoneNumberVerified] <- box phoneNumberVerified

            if ctx.User.HasScope(OpenIddictConstants.Scopes.Roles) then
                let! roles = userManager.GetRolesAsync(user)
                claims.[OpenIddictConstants.Claims.Role] <- box roles

            return! Successful.OK claims next ctx
    }


let logout: HttpHandler =
    fun _ ctx -> task {
        let signInManager = Ioc.signInManager ctx
        do! signInManager.SignOutAsync()

        do! ctx.SignOutAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, AuthenticationProperties(RedirectUri = "/"))

        return Some ctx
    }


let routes = router {
    get "/connect/authorize" authorize
    get "/connect/google" googleLogin
    get "/connect/google/complete" googleResponse
    get "/connect/logout" logout
    post "/connect/token" exchangeToken
    get "/connect/userinfo" userinfo
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
            options.ClientId <- googleSection.["ClientId"]
            options.ClientSecret <- googleSection.["ClientSecret"]
            options.CallbackPath <- PathString "/connect/google/callback"
            options.SignInScheme <- IdentityConstants.ExternalScheme)
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
            options.SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
                .SetUserinfoEndpointUris("/connect/userinfo")
                .SetLogoutEndpointUris("/connect/logout")
            |> ignore

            options.RegisterScopes(OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Roles) |> ignore
            options.AllowAuthorizationCodeFlow() |> ignore
            options.AddDevelopmentEncryptionCertificate() |> ignore
            options.AddDevelopmentSigningCertificate() |> ignore
            options.RemoveEventHandler(OpenIddictServerAspNetCoreHandlers.ValidateTransportSecurityRequirement.Descriptor) |> ignore
            options.DisableAccessTokenEncryption() |> ignore

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
    app.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = ForwardedHeaders.All)) |> ignore
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

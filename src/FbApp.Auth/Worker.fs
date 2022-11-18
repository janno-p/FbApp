namespace FbApp.Auth


open Microsoft.AspNetCore.Identity
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open OpenIddict.Abstractions
open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Identity.EntityFrameworkCore


type ClientTypes = OpenIddictConstants.ClientTypes
type ConsentTypes = OpenIddictConstants.ConsentTypes
type Permissions = OpenIddictConstants.Permissions
type Requirements = OpenIddictConstants.Requirements


[<AllowNullLiteral>]
type ApplicationUser() =
    inherit IdentityUser<Guid>()
    member val PictureUrl = Unchecked.defaultof<string> with get, set
    member val Provider = Unchecked.defaultof<string> with get, set
    member val ProviderId = Unchecked.defaultof<string> with get, set
    member val GivenName = Unchecked.defaultof<string> with get, set
    member val Surname = Unchecked.defaultof<string> with get, set
    member val FullName = Unchecked.defaultof<string> with get, set

[<AllowNullLiteral>]
type ApplicationRole() =
    inherit IdentityRole<Guid>()


type ApplicationDbContext(options: DbContextOptions<ApplicationDbContext>) =
    inherit IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)


type Worker(serviceProvider: IServiceProvider, configuration: IConfiguration) =
    interface IHostedService with
        member _.StartAsync _ = task {
            use scope = serviceProvider.CreateScope()

            let context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            let! _ = context.Database.EnsureCreatedAsync()

            let manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>()

            let redirectUris = ResizeArray<string>()
            configuration.Bind("Authentication:RedirectUris", redirectUris)

            let descriptor =
                OpenIddictApplicationDescriptor(
                    ClientId = "fbapp-ui-client",
                    ConsentType = ConsentTypes.Implicit,
                    DisplayName = "FbApp UI Application",
                    Type = ClientTypes.Public
                )

            redirectUris
            |> Seq.iter (fun redirectUri ->
                descriptor.PostLogoutRedirectUris.Add(Uri(redirectUri)) |> ignore
                descriptor.RedirectUris.Add(Uri(redirectUri)) |> ignore
            )

            descriptor.Permissions.Add(Permissions.Endpoints.Authorization) |> ignore
            descriptor.Permissions.Add(Permissions.Endpoints.Logout) |> ignore
            descriptor.Permissions.Add(Permissions.Endpoints.Token) |> ignore
            descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode) |> ignore
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken) |> ignore
            descriptor.Permissions.Add(Permissions.ResponseTypes.Code) |> ignore
            descriptor.Permissions.Add(Permissions.Scopes.Email) |> ignore
            descriptor.Permissions.Add(Permissions.Scopes.Profile) |> ignore
            descriptor.Permissions.Add(Permissions.Scopes.Roles) |> ignore

            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange) |> ignore

            match! manager.FindByClientIdAsync("fbapp-ui-client") with
            | null ->
                let! _ = manager.CreateAsync(descriptor)
                ()
            | app ->
                let! _ = manager.UpdateAsync(app, descriptor)
                ()

            let roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            let! adminRoleExists = roleManager.RoleExistsAsync("admin")
            if not adminRoleExists then
                let adminRole = ApplicationRole(Name = "admin")
                let! _ = roleManager.CreateAsync(adminRole)
                ()
        }

        member _.StopAsync _ =
            Task.CompletedTask

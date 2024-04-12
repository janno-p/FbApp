module internal FbApp.Modules.UserAccess.Models

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Identity.EntityFrameworkCore
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting


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


type UserAccessDbContext(options: DbContextOptions<UserAccessDbContext>) =
    inherit IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)


type UserAccessDbInitializer(serviceProvider: IServiceProvider) =
    interface IHostedService with
        member _.StartAsync _ = task {
            use scope = serviceProvider.CreateScope()

            let context = scope.ServiceProvider.GetRequiredService<UserAccessDbContext>()
            let! _ = context.Database.EnsureCreatedAsync()

            let roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            let! adminRoleExists = roleManager.RoleExistsAsync("admin")
            if not adminRoleExists then
                let adminRole = ApplicationRole(Name = "admin")
                let! _ = roleManager.CreateAsync(adminRole)
                ()
        }

        member _.StopAsync _ =
            Task.CompletedTask

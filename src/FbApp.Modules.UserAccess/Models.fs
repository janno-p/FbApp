module internal FbApp.Modules.UserAccess.Models

open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Identity.EntityFrameworkCore
open Microsoft.EntityFrameworkCore
open System


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

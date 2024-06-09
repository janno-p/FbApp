module internal FbApp.Modules.Competitions.Database

open Microsoft.EntityFrameworkCore


type CompetitionsDbContext(options: DbContextOptions<CompetitionsDbContext>) =
    inherit DbContext(options)

    override _.OnModelCreating(builder) =
        base.OnModelCreating(builder)
        builder.HasDefaultSchema("competitions") |> ignore

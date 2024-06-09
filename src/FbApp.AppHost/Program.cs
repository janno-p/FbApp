var builder = DistributedApplication.CreateBuilder(args);

builder.AddRedis("cache");

builder.AddExecutable(
    "npx",
    "tailwindcss",
    Path.Combine(Environment.CurrentDirectory, ".."),
    "-i", Path.Combine("FbApp.Modules.WebApp", "styles", "app.tailwind.css"),
    "-o", Path.Combine("FbApp", "wwwroot", "css", "app.css"),
    "-c", "tailwind.config.cjs",
    "--minify",
    "--watch=always");

var database = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("database");

builder.AddProject<Projects.FbApp>("service")
    .WithHttpsEndpoint(8090, name: "https")
    .WithReference(database)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");

builder.AddProject<Projects.FbApp_DbManager>("dbmanager")
    .WithReference(database);

builder.Build().Run();

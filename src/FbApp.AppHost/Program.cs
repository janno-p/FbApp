var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

builder.AddExecutable(
    "tailwindcss",
    "tailwindcss",
    Path.Combine(Environment.CurrentDirectory, ".."),
    "-i", Path.Combine("FbApp.Modules.WebApp", "styles", "app.tailwind.css"),
    "-o", Path.Combine("FbApp", "wwwroot", "css", "app.css"),
    "-c", "tailwind.config.js",
    "--minify");

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("fbapp-user-access");

var service = builder.AddProject<Projects.FbApp>("service")
    .WithHttpsEndpoint(8090, name: "https")
    .WithReference(postgres)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");

builder.Build().Run();

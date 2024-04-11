var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("fbapp");

var apiService = builder.AddProject<Projects.FbApp_Api>("apiservice")
    .WithHttpsEndpoint(8090, name: "https")
    .WithReference(postgres);

// builder.AddProject<Projects.Xyz_Web>("webfrontend")
//     .WithReference(cache)
//     .WithReference(apiService);

builder.Build().Run();

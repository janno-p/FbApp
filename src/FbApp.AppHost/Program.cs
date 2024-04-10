var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.FbApp_Api>("apiservice")
    .WithHttpsEndpoint(5141, name: "https");

// builder.AddProject<Projects.Xyz_Web>("webfrontend")
//     .WithReference(cache)
//     .WithReference(apiService);

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.FbApp_Api>("apiservice")
    .WithHttpEndpoint(5141, "http");

// builder.AddProject<Projects.Xyz_Web>("webfrontend")
//     .WithReference(cache)
//     .WithReference(apiService);

builder.Build().Run();

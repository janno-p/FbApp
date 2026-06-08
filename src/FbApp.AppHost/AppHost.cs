var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("fbapp-pgdb");
var authDatabase = postgres.AddDatabase("fbapp-pgdb-auth");

var mongo = builder.AddMongoDB("fbapp-mongodb");
var apiDatabase = mongo.AddDatabase("fbapp-mongodb-api");

var kurrentdb = builder.AddKurrentDB("fbapp-kurrentdb");

var apiService = builder
    .AddProject<Projects.FbApp_Api>("fbapp-api", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint(10202)
    .WithReference(kurrentdb, "eventstore")
    .WithReference(apiDatabase, "mongodb")
    .WaitFor(kurrentdb)
    .WaitFor(apiDatabase);

var authService = builder
    .AddProject<Projects.FbApp_Auth>("fbapp-auth", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint(10203)
    .WithReference(authDatabase, "postgres")
    .WaitFor(authDatabase);

var proxyService = builder
    .AddProject<Projects.FbApp_Proxy>("fbapp-proxy", options => options.ExcludeLaunchProfile = true)
    .WithReference(apiService)
    .WithReference(authService)
    .WithHttpEndpoint(10201);

var webClient = builder.AddBunApp("fbapp-web", "../FbApp.Web", "dev")
    .WithEnvironment("__VITE_ADDITIONAL_SERVER_ALLOWED_HOSTS", "aspire.dev.internal")
    .WithHttpEndpoint(5173, 5173, isProxied: false);

var ingress = builder.AddYarp("ingress")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/.well-known/{**catch-all}", proxyService);
        yarp.AddRoute("/api/{**catch-all}", proxyService);
        yarp.AddRoute("/connect/{**catch-all}", proxyService);
        yarp.AddRoute("/{**catch-all}", webClient);
    })
    .WithHostHttpsPort(8090);

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("fbapp-pgdb"); //.WithDataVolume();
var authDatabase = postgres.AddDatabase("fbapp-pgdb-auth");

var mongo = builder.AddMongoDB("fbapp-mongodb"); //.WithDataVolume();
var apiDatabase = mongo.AddDatabase("fbapp-mongodb-api");

var kurrentdb = builder.AddKurrentDB("fbapp-kurrentdb"); //.WithDataVolume();

var apiServiceConfig = builder.Configuration.GetSection("Services:ApiService");

var apiService = builder
    .AddProject<Projects.FbApp_Api>("fbapp-api", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint(10202)
    .WithReference(kurrentdb, "eventstore")
    .WithReference(apiDatabase, "mongodb")
    .WaitFor(kurrentdb)
    .WaitFor(apiDatabase);

var footballDataToken = apiServiceConfig["FootballDataToken"];
if (!string.IsNullOrEmpty(footballDataToken))
{
    apiService.WithEnvironment("Authentication__FootballDataToken", footballDataToken);
}

var authServiceConfig = builder.Configuration.GetSection("Services:AuthService");

var authService = builder
    .AddProject<Projects.FbApp_Auth>("fbapp-auth", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint(10203)
    .WithReference(authDatabase, "postgres")
    .WaitFor(authDatabase);

var googleClientId = authServiceConfig["ClientId"];
if (!string.IsNullOrEmpty(googleClientId))
{
    authService.WithEnvironment("Google__Authentication__ClientId", googleClientId);
}

var googleClientSecret = authServiceConfig["ClientSecret"];
if (!string.IsNullOrEmpty(googleClientSecret))
{
    authService.WithEnvironment("Google__Authentication__ClientSecret", googleClientSecret);
}

var defaultAdmin = authServiceConfig["DefaultAdmin"];
if (!string.IsNullOrEmpty(defaultAdmin))
{
    authService.WithEnvironment("Authorization__DefaultAdmin", defaultAdmin);
}

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

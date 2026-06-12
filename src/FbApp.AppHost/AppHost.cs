var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("fbapp-pgdb"); //.WithDataVolume();
var authDatabase = postgres.AddDatabase("fbapp-pgdb-auth");

var mongo = builder.AddMongoDB("fbapp-mongodb"); //.WithDataVolume();
var apiDatabase = mongo.AddDatabase("fbapp-mongodb-api");

const string KurrentDbDataVolume = "fbapp-kurrentdb-data";
const string KurrentDbRegistry = "docker.kurrent.io";
const string KurrentDbImage = "kurrent-latest/kurrentdb";
const string KurrentDbTag = "26.1";

var kurrentConfig = builder.Configuration.GetSection("Services:KurrentDB");
var kurrentRestorePath = kurrentConfig["RestorePath"];

var kurrentdb = builder.AddKurrentDB("fbapp-kurrentdb")
    .WithImage(KurrentDbImage, KurrentDbTag)
    .WithImageRegistry(KurrentDbRegistry)
    .WithDataVolume(KurrentDbDataVolume);

if (!string.IsNullOrEmpty(kurrentRestorePath))
{
    var scriptsPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "Scripts"));

    var restoreKurrentDb = builder.AddContainer("fbapp-kurrentdb-restore", "alpine", "3.20")
        .WithBindMount(kurrentRestorePath, "/backup", isReadOnly: true)
        .WithBindMount(scriptsPath, "/scripts", isReadOnly: true)
        .WithVolume(KurrentDbDataVolume, "/var/lib/kurrentdb")
        .WithEnvironment("BACKUP_DIR", "/backup")
        .WithEnvironment("DATA_DIR", "/var/lib/kurrentdb")
        .WithEnvironment("KURRENT_UID", "1001")
        .WithEnvironment("KURRENT_GID", "1001")
        .WithEntrypoint("/bin/sh")
        .WithArgs("/scripts/restore-kurrentdb.sh");

    var prepareKurrentDb = builder
        .AddContainer("fbapp-kurrentdb-prepare", KurrentDbImage, KurrentDbTag)
        .WithImageRegistry(KurrentDbRegistry)
        .WithVolume(KurrentDbDataVolume, "/var/lib/kurrentdb")
        .WithEnvironment("KURRENTDB_INSECURE", "true")
        .WithEnvironment("KURRENTDB_RUN_PROJECTIONS", "All")
        .WaitForCompletion(restoreKurrentDb);

    kurrentdb.WaitForCompletion(prepareKurrentDb);
}

var apiServiceConfig = builder.Configuration.GetSection("Services:ApiService");

var apiService = builder
    .AddProject<Projects.FbApp_Api>("fbapp-api", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint(10202)
    .WithEnvironment("EventStore__Subscriptions__Reset", "true")
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

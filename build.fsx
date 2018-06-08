#r "paket: groupref Build //"
#load "./.fake/build.fsx/intellisense.fsx"

open EventStore.ClientAPI
open EventStore.ClientAPI.Projections
open EventStore.ClientAPI.SystemData
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.JavaScript
open Org.BouncyCastle.Asn1.X509
open Org.BouncyCastle.Crypto
open Org.BouncyCastle.Crypto.Generators
open Org.BouncyCastle.Crypto.Operators
open Org.BouncyCastle.Crypto.Prng
open Org.BouncyCastle.Math
open Org.BouncyCastle.Pkcs
open Org.BouncyCastle.Security
open Org.BouncyCastle.Utilities
open Org.BouncyCastle.X509
open System
open System.Net
open System.IO

let [<Literal>] ApplicationName = "FbApp"
let [<Literal>] KeyStrength = 2048
let [<Literal>] SignatureAlgorithm = "SHA256WithRSA"
let [<Literal>] SubjectName = "CN=localhost"

let srcDir = __SOURCE_DIRECTORY__ </> "src"
let certificatePath = srcDir </> (sprintf "%s.pfx" ApplicationName)

let clientPath = "./src/Client" |> Path.getFullName
let serverPath = "./src/Server" |> Path.getFullName

let dotnetCliVersion = DotNet.getSDKVersionFromGlobalJson()

let install =
    lazy DotNet.install (fun opt -> { opt with Version = DotNet.Version dotnetCliVersion })

let inline withWorkingDir wd =
    DotNet.Options.lift install.Value
    >> DotNet.Options.withWorkingDirectory wd

Target.create "Run" (fun _ ->
    let client = async {
        Yarn.exec "quasar dev" (fun o -> { o with WorkingDirectory = clientPath })
    }
    let server = async {
        Environment.setEnvironVar "ASPNETCORE_ENVIRONMENT" "Development"
        let result = DotNet.exec (withWorkingDir serverPath) "watch" "run"
        if not result.OK then
            failwithf "'watch run' failed with errors %A." result.Errors
    }
    [ client; server ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
)

Target.create "GenerateCertificate" (fun _ ->
    let randomGenerator = CryptoApiRandomGenerator()
    let random = SecureRandom(randomGenerator)

    let certificateGenerator = X509V3CertificateGenerator()

    let serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(System.Int64.MaxValue), random)
    certificateGenerator.SetSerialNumber(serialNumber)

    let subjectDn = X509Name(SubjectName)
    let issuerDn = subjectDn
    certificateGenerator.SetIssuerDN(issuerDn)
    certificateGenerator.SetSubjectDN(subjectDn)

    let now = DateTime.UtcNow
    let notBefore = now.Date
    let notAfter = notBefore.AddYears(2)
    certificateGenerator.SetNotBefore(notBefore)
    certificateGenerator.SetNotAfter(notAfter)

    let keyGenerationParameters = KeyGenerationParameters(random, KeyStrength)
    let keyPairGenerator = RsaKeyPairGenerator()
    keyPairGenerator.Init(keyGenerationParameters)
    let subjectKeyPair = keyPairGenerator.GenerateKeyPair()

    certificateGenerator.SetPublicKey(subjectKeyPair.Public)

    let issuerKeyPair = subjectKeyPair
    let certificate = certificateGenerator.Generate(Asn1SignatureFactory(SignatureAlgorithm, issuerKeyPair.Private, random))

    let store = Pkcs12Store()
    let friendlyName = certificate.SubjectDN.ToString()

    let certificateEntry = X509CertificateEntry(certificate)
    store.SetCertificateEntry(friendlyName, certificateEntry)

    store.SetKeyEntry(friendlyName, AsymmetricKeyEntry(subjectKeyPair.Private), [| certificateEntry |])

    use stream = new MemoryStream()
    store.Save(stream, [||], random)

    stream.Position <- 0L
    File.WriteAllBytes(certificatePath, stream.ToArray())
)

Target.create "SetupEventStore" (fun _ ->
    let settings =
        ConnectionSettings
            .Create()
            .UseConsoleLogger()
            .SetDefaultUserCredentials(UserCredentials("admin", "changeit"))
            .Build()

    let connection = EventStoreConnection.Create(settings, System.Uri("tcp://localhost:1113"))

    async {
        do! (connection.ConnectAsync()
             |> Async.AwaitTask)

        let projectionsManager = ProjectionsManager(Common.Log.ConsoleLogger(), DnsEndPoint("localhost", 2113), TimeSpan.FromSeconds(5.0))

        let query = """fromAll()
.when({
    $any: function (state, ev) {
        if (ev.metadata !== null && ev.metadata.applicationName === "FbApp") {
            linkTo("domain-events", ev)
        }
    }
})"""

        try
            do! (projectionsManager.CreateContinuousAsync("domain-events", query, UserCredentials("admin", "changeit"))
                 |> Async.AwaitTask)
        with e -> Trace.tracefn "%A" (e.ToString())

        let settings = PersistentSubscriptionSettings.Create().ResolveLinkTos().Build()

        try
            do! (connection.CreatePersistentSubscriptionAsync("domain-events", "projections", settings, null)
                 |> Async.AwaitTask)
        with e -> Trace.tracefn "%A" (e.ToString())

        try
            do! (connection.CreatePersistentSubscriptionAsync("domain-events", "process-manager", settings, null)
                 |> Async.AwaitTask)
        with e -> Trace.tracefn "%A" (e.ToString())
    } |> Async.RunSynchronously
)

"GenerateCertificate"
    =?> ("Run", not (File.exists certificatePath))

Target.runOrDefault "Run"

namespace FbApp.Server

open Microsoft.Extensions.DependencyInjection

module XsrfToken =
    open Microsoft.AspNetCore.Antiforgery
    open Microsoft.AspNetCore.Http

    let [<Literal>] CookieName = "XSRF-TOKEN"

    let create (context: HttpContext) =
        let antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>()
        let tokens = antiforgery.GetAndStoreTokens(context)
        tokens.RequestToken

    let refresh (context: HttpContext) =
        context |> create |> ignore

module HttpsConfig =
    open System.Net
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.Server.Kestrel.Core
    open System
    open System.Security.Cryptography.X509Certificates

    type EndpointScheme =
        | Http
        | Https

    type EndpointConfiguration =
        {
            Host: string
            Port: int option
            Scheme: EndpointScheme
            FilePath: string option
            Password: string option
            StoreName: string option
            StoreLocation: string option
        }
        static member Default =
            {
                Host = "localhost"
                Port = Some 8080
                Scheme = Http
                FilePath = None
                Password = None
                StoreName = None
                StoreLocation = None
            }

    let loadCertificateFromStore (storeName: string) location cfg (env: IHostingEnvironment) =
        use store = new X509Store(storeName, Enum.Parse<StoreLocation> location)
        store.Open(OpenFlags.ReadOnly)
        let cert = store.Certificates.Find(X509FindType.FindBySubjectName, cfg.Host, not (env.IsDevelopment()))
        if cert.Count = 0 then raise (InvalidOperationException(sprintf "Certificate not found for %s." cfg.Host)) else
        cert.[0]

    let loadCertificate cfg env =
        match cfg.StoreName, cfg.StoreLocation, cfg.FilePath, cfg.Password with
        | Some n, Some l, _, _ -> loadCertificateFromStore n l cfg env
        | _, _, Some f, Some p -> new X509Certificate2(f, p)
        | _, _, Some f, None -> new X509Certificate2(f)
        | _ -> raise (InvalidOperationException("No valid certificate configuration found for the current endpoint."))

    type KestrelServerOptions with
        member this.ConfigureEndpoints (endpoints: EndpointConfiguration list) =
            let env = this.ApplicationServices.GetRequiredService<IHostingEnvironment>()
            endpoints
            |> List.choose (fun endpoint -> if env.IsProduction() && endpoint.Scheme = Https then None else Some(endpoint))
            |> List.iter (fun endpoint ->
                let port =
                    match endpoint.Port with
                    | Some p -> p
                    | None -> if endpoint.Scheme.Equals "https" then 443 else 80

                let ipAddresses =
                    if endpoint.Host.Equals "localhost" then [ IPAddress.IPv6Loopback; IPAddress.Loopback ] else
                    match IPAddress.TryParse endpoint.Host with
                    | true, ip -> [ ip ]
                    | _ -> [ IPAddress.IPv6Any ]

                ipAddresses
                |> List.iter (fun ip ->
                    this.Listen(ip, port, fun options ->
                        match endpoint.Scheme with
                        | Https ->
                            loadCertificate endpoint env
                            |> options.UseHttps
                            |> ignore
                        | Http -> ()))
            )

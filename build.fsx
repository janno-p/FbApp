#r "paket:
nuget Fake.Core.Target //"

#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.IO.FileSystemOperators

let kubectlExec (command: string) (env: string) =
    Trace.trace $"Applying kubernetes configuration to %s{env}"

    let input = StreamRef.Empty

    let kubectl =
        [command; "-f"; "-"]
        |> CreateProcess.fromRawCommand "kubectl"
        |> CreateProcess.withStandardInput (CreatePipe input)
        |> Proc.start

    let kustomize =
        ["build"; __SOURCE_DIRECTORY__ </> "kustomize" </> "overlays" </> env]
        |> CreateProcess.fromRawCommand "kustomize"
        |> CreateProcess.withStandardOutput (UseStream (true, input.Value))
        |> Proc.run

    kubectl.Wait()

    if kustomize.ExitCode <> 0 then
        failwith "Failed result from Kustomize"

    if kubectl.Result.ExitCode <> 0 then
        failwith "Failed result from Kubectl"

let deployDockerImage (name: string) =
    Trace.trace $"Building docker image: %s{name}"

    let result =
        ["build"; "-t"; $"localhost:32000/%s{name}:registry"; "-f"; $"%s{name}/Dockerfile"; __SOURCE_DIRECTORY__]
        |> CreateProcess.fromRawCommand "docker"
        |> Proc.run

    if result.ExitCode <> 0 then
        failwith "Failed result from Docker"

    let result =
        ["push"; $"localhost:32000/%s{name}:registry"]
        |> CreateProcess.fromRawCommand "docker"
        |> Proc.run

    if result.ExitCode <> 0 then
        failwith "Failed result from Docker"

Target.create "fbapp-api" (fun _ ->
    deployDockerImage "fbapp-api"
)

Target.create "fbapp-init-events" (fun _ ->
    deployDockerImage "fbapp-init-events"
)

Target.create "fbapp-live-update" (fun _ ->
    deployDockerImage "fbapp-live-update"
)

Target.create "fbapp-ui" (fun _ ->
    deployDockerImage "fbapp-ui"
)

Target.create "delete-production" (fun _ ->
    kubectlExec "delete" "production"
)

Target.create "update-production" (fun _ ->
    kubectlExec "apply" "production"
)

Target.create "delete-staging" (fun _ ->
    kubectlExec "delete" "staging"
)

Target.create "update-staging" (fun _ ->
    kubectlExec "apply" "staging"
)

Target.runOrList ()

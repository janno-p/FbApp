module FbApp.Modules

open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.Extensions.Configuration


module UserAccessModule = FbApp.Modules.UserAccess.Module


type ModuleRequirement =
    | RequiresAuthorization
    | RequiresForwardedHeaders of ForwardedHeaders


type ApplicationModule = {
    Name: string
    ConfigureServices: WebApplicationBuilder -> unit
    Endpoints: Endpoint list
    Requirements: ModuleRequirement list
}


let private applicationModules: ApplicationModule list = [
    {
        Name = "UserAccess"
        ConfigureServices = UserAccessModule.configureServices
        Endpoints = UserAccessModule.endpoints
        Requirements = [
            RequiresForwardedHeaders (ForwardedHeaders.XForwardedHost ||| ForwardedHeaders.XForwardedProto)
        ]
    }
]


let getEnabledModules (configuration: IConfiguration) =
    let modulesSection = configuration.GetRequiredSection("Modules")
    modulesSection.GetChildren()
    |> Seq.choose (fun section -> applicationModules |> List.tryFind (fun x -> x.Name = section.Key && section.GetValue("Enabled")))
    |> Seq.toList

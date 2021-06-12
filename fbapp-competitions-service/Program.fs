module FbApp.Competitions.Program


open Giraffe
open Microsoft.Extensions.DependencyInjection
open Saturn


let routes = router {
    get "" Api.getActiveCompetitionApi
    getf "%i" Api.getCompetitionSourcesApi
}


let configureServices: IServiceCollection -> IServiceCollection =
    fun services ->
        services


let app = application {
    service_config configureServices
    use_router (router {
        not_found_handler (RequestErrors.NOT_FOUND "Not Found")
        forward "/api/competitions" routes
    })
}


run app

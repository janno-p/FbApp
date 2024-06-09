module FbApp.Modules.Competitions.Module

let endpoints = [
    yield! AddCompetition.endpoints
    yield! ListCompetitions.endpoints
]

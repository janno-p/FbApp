# FbApp #

Playground application for trying out new technologies, libraries, frameworks, patterns, tools etc.


## Primary Objectives ##

* Local development environment using [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/).
* Back-end built with [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) and [F# Programming Language](https://fsharp.org).
* Front-end built using [HTMX](https://htmx.org).


## Prerequisites ##

### Development environment ###

* [.NET Core 8.0 SDK](https://www.microsoft.com/net/download)

### Install .NET Aspire workload

```sh
$ dotnet workload update
$ dotnet workload install aspire
```

### Install TailwindCSS command line tool

```sh
$ scoop install tailwindcss
```


## Quick Start ##

### Configure Google OAuth authentication

Use Google Developer Console to register new application for Google authentication:

* Authorized JavaScript Origins: `https://localhost:8090`
* Authorized redirect URIs: `https://localhost:8090/user-access/google/callback`

Add `src/FbApp.Auth/appsettings.user.json` configuration file with credentials provided
by Google client application registration.

```json
{
  "Modules": {
    "UserAccess": {
      "Google": {
        "ClientId": "<redacted>",
        "ClientSecret": "<redacted>"
      }
    }
  }
}
```


### Run development environment

```sh
$ dotnet run --project src/FbApp.AppHost/FbApp.AppHost.csproj
```

Open .NET Aspire [Dashboard](http://localhost:15090)


## Project structure

Project consists of single monolithic application which is divided into multiple
independent modules. Each module is designed to work as separate service which could
be deployed as individual microservice. By default, all modules run inside same application
context. Each module may have its own architectural style.

### Modules

#### UserAccess module

UserAccess module is responsible for authentication, authorization and other user
management related functionalities.

#### WebApp module

Primary entry point of the front-end application.

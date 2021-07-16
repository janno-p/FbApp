# FbApp #

Playground application for trying out new technologies, libraries, frameworks, patterns, tools etc.


## Primary Objectives ##

* Authentication using [Google Sign-In JavaScript Client](https://developers.google.com/identity/sign-in/web/reference).
* Client application using [Vue.js](https://vuejs.org/) and [Quasar Framework](http://quasar-framework.org).
* Server API built with [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) and [F# Programming Language](https://fsharp.org).
* Apply [CQRS](https://martinfowler.com/bliki/CQRS.html) and [Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html) principles.


## Prerequisites ##

* [.NET Core SDK](https://www.microsoft.com/net/download)
* [Node.js](https://nodejs.org/en/)
* [Yarn](https://yarnpkg.com/en/)
* [Dapr](https://dapr.io)
* [Docker Desktop](https://docs.docker.com/docker-for-windows/install/)


## Quick Start ##

Install and initialize local dapr:

* https://docs.dapr.io/getting-started/install-dapr-cli/
* https://docs.dapr.io/getting-started/install-dapr-selfhost/

Restore required tools

```sh
$ dotnet tool restore
```

Restore node dependencies

```sh
$ cd fbapp-ui
$ yarn
$ cd ..
```

Use Google Developer Console to register new application for Google authentication:

* Authorized JavaScript Origins: `https://localhost:8090`
* Authorized redirect URIs: `https://localhost:8090/connect/google/callback`

Add `fbapp-auth-service/appsettings.user.json` configuration file with credentials provided
by Google client application registration.

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "<redacted>",
      "ClientSecret": "<redacted>"
    }
  }
}
```

Run development environment

```sh
$ dotnet tye run
```

Open [Tye Dashboard](http://localhost:8000) to monitor running components.

Open [Application](https://localhost:8090) for demo.

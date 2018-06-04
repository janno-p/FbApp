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
* [MongoDB](https://www.mongodb.com/)
* [EventStore](https://eventstore.org/)


## Quick Start ##

Make sure EventStore and MongoDB instances are running:

```
EventStore.ClusterNode.exe --db ./db --log ./logs --run-projections=all --start-standard-projections=true
"C:\Program Files\MongoDB\Server\3.6\bin\mongod.exe"
```

Prepare build environment:

```
.paket\paket.exe install
dotnet tool install fake-cli -g --version 5.0.0-rc018*
dotnet restore FbApp.sln
```

Build and run application:

```
dotnet fake run build.fsx
```

The [site](http://localhost:8080) should automatically open in default browser.

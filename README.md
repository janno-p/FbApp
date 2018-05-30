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


## Quick Start ##

Build and run application:

```
dotnet restore FbApp.sln
dotnet fake run build.fsx
```

The [site](http://localhost:8080) should automatically open in default browser.

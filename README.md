# FbApp #

Playground application for trying out new technologies, libraries, frameworks, patterns, tools etc.


## Primary Objectives ##

* Client application using [Elm](https://elm-lang.org/) and [Vite](https://vitejs.dev/).
* Server API built with [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) and [F# Programming Language](https://fsharp.org).
* Apply [CQRS](https://martinfowler.com/bliki/CQRS.html) and [Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html) principles to [Microservices](https://microservices.io/) architecture.


## Prerequisites ##

### Run local application ###

* [Docker Desktop with Kubernetes enabled](https://docs.docker.com/docker-for-windows/install/)
* [Tilt](https://tilt.dev/)

### Development environment ###

* [.NET Core SDK](https://www.microsoft.com/net/download)
* [Node.js](https://nodejs.org/en/)
* [Yarn](https://yarnpkg.com/en/)


## Quick Start ##

### Configure Google OAuth authentication

Use Google Developer Console to register new application for Google authentication:

* Authorized JavaScript Origins: `https://localhost:8090`
* Authorized redirect URIs: `https://localhost:8090/connect/google/callback`

Add `src/FbApp.Auth/appsettings.user.json` configuration file with credentials provided
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

### Configure kubernetes cluster

Install dapr

```
helm repo add dapr https://dapr.github.io/helm-charts/

helm repo update

helm upgrade --install dapr dapr/dapr \
--namespace dapr-system \
--create-namespace \
--wait

kubectl port-forward service/dapr-dashboard 8080:8080 --namespace dapr-system
```

Install ingress controller

```
helm repo add dapr https://kubernetes.github.io/ingress-nginx

helm repo update

helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
--namespace ingress-nginx \
--create-namespace \
--set controller.config.use-forwarded-headers=true
--set controller.service.ports.https=8090
--wait

kubectl port-forward service/ingress-nginx-controller 8090:443 --namespace ingress-nginx
```


### Run development environment

```sh
$ tilt up
```

Open [Tilt Dashboard](http://localhost:10350/) to monitor running components.

Open [Application](https://localhost:8090) for demo.

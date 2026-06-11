# FbApp

FbApp is a 2026 football prediction application and playground for the current .NET, F#, Elm, Aspire, and event-sourced architecture used in this repository.

The app lets authenticated users submit competition predictions, including group-stage rankings, third-place qualification, knockout fixtures, top-scorer picks, and boosters. It also exposes fixture views, prediction status, leaderboards, leagues, and admin dashboard endpoints.

## Architecture

- `src/FbApp.Web` is a Bun-managed Vite/Elm client.
- `src/FbApp.Proxy` is the YARP front door for `/api/*`, `/connect/*`, and `/.well-known/*`.
- `src/FbApp.Api` is an ASP.NET Core/Giraffe F# API using MongoDB read models, KurrentDB event streams, and Quartz live-update jobs.
- `src/FbApp.Auth` hosts OpenIddict, Google OAuth, ASP.NET Core Identity, and PostgreSQL-backed auth state.
- `src/FbApp.AppHost` starts the local Aspire environment with PostgreSQL, MongoDB, KurrentDB, API, Auth, Proxy, web client, and HTTPS ingress on `https://localhost:8090`.
- `chart/` contains the Kubernetes/Helm deployment for API, Auth, Proxy, web, MongoDB, PostgreSQL, and EventStore/KurrentDB-compatible event storage. Proxy routing uses configured service addresses directly.

## Prerequisites

- [Docker Desktop](https://docs.docker.com/docker-for-windows/install/) or another Docker-compatible runtime for local infrastructure and integration tests.
- [.NET SDK](https://www.microsoft.com/net/download) with the SDK required by the solution.
- [.NET Aspire CLI](https://learn.microsoft.com/dotnet/aspire/cli/overview) for local orchestration.
- [Bun](https://bun.sh/) for the web client.

## Quick Start

### Configure Google OAuth Authentication

Use Google Developer Console to register new application for Google authentication:

* Authorized JavaScript Origins: `https://localhost:8090`
* Authorized redirect URIs: `https://localhost:8090/connect/google/callback`

Add `src/FbApp.Auth/appsettings.user.json` configuration file with credentials provided
by Google client application registration.

```json
{
  "Google": {
    "Authentication": {
      "ClientId": "<redacted>",
      "ClientSecret": "<redacted>"
    }
  },
  "Authorization": {
    "DefaultAdmin": "<optional admin email>"
  }
}
```

When running through Aspire, the same values can be supplied from `src/FbApp.AppHost` configuration as `Services:AuthService:ClientId`, `Services:AuthService:ClientSecret`, and `Services:AuthService:DefaultAdmin`. Football-Data API access is configured as `Services:ApiService:FootballDataToken` in AppHost configuration or `Authentication:FootballDataToken` in API configuration.

### Run Development Environment

```sh
aspire run
```

Open the Aspire dashboard URL printed by `aspire run` to monitor running components.

Open [Application](https://localhost:8090) for demo.

## Common Commands

Restore and build the backend solution from the repo root:

```sh
dotnet restore FbApp.slnx
dotnet build FbApp.slnx
```

Run backend tests:

```sh
dotnet test --solution FbApp.slnx
```

Run the web client from `src/FbApp.Web`:

```sh
bun install
bun run dev
bun run build
```

## Public Routes

- Web routes include `/`, `/login`, `/logout`, `/changelog`, `/prediction`, `/fixture`, `/fixture/:id`, and `/leaderboard`.
- API routes include `/api/competition/status`, `/api/predict/fixtures`, `/api/predict`, `/api/predict/third-place-matchups`, `/api/prediction`, `/api/prediction/board`, `/api/fixtures`, and `/api/fixtures/{id}`.
- Admin-only API routes are grouped under `/api/dashboard`, `/api/predictions/admin`, and `/api/leagues/admin`.

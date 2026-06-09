# AGENTS.md

## Repo Shape

- The real .NET workspace is `FbApp.slnx` at the repo root. It contains F# `net10.0` services `FbApp.Api`, `FbApp.Auth`, `FbApp.Proxy`, C# `FbApp.AppHost`/`FbApp.ServiceDefaults`, plus `FbApp.Api.IntegrationTests` and `FbApp.Auth.IntegrationTests`.
- The web client is separate under `src/FbApp.Web`; the repo root has no `package.json`. Run web package commands from `src/FbApp.Web`.
- `Directory.Packages.props` centrally owns NuGet package versions. Do not add package versions to individual `.fsproj` files unless central package management is being changed.
- F# file order in each `.fsproj` is compile order. When adding or moving F# files, update the relevant `<Compile Include=...>` sequence intentionally.

## Commands

- Restore/build backend: `dotnet restore FbApp.slnx` then `dotnet build FbApp.slnx`.
- Run all backend tests: `dotnet test --solution FbApp.slnx`.
- Run one backend test project: `dotnet test src/FbApp.Auth.IntegrationTests/FbApp.Auth.IntegrationTests.fsproj`.
- Web install/build/dev: from `src/FbApp.Web`, use `bun install`, `bun run build`, and `bun run dev`. The web package is Bun-managed and keeps `bun.lock` in sync with dependency changes.
- There are no repo-level lint, formatter, typecheck, or web test scripts in the checked configs.

## Local Runtime

- Full local app startup is Aspire based: run `aspire run` from the repo root to start `FbApp.AppHost` and its resources.
- `FbApp.AppHost` wires PostgreSQL, MongoDB, KurrentDB, the API/Auth/Proxy services, the Bun web client, and YARP ingress on `https://localhost:8090`.
- Dockerfiles for the individual backend services must copy `src/FbApp.ServiceDefaults` because each service project references it.
- Google OAuth local setup uses `src/FbApp.Auth/appsettings.user.json`; keep secrets out of git.

## Architecture Notes

- `FbApp.Proxy` is the front door for `/api/*`, `/connect/*`, and `/.well-known/*`. Its YARP config rewrites `dapr:` destinations using `DAPR_HTTP_PORT` unless a connection string override exists.
- `FbApp.Api` is a Giraffe app using MongoDB, KurrentDB, Dapr, and Quartz; main routes and startup wiring live in `src/FbApp.Api/Main.fs`.
- `FbApp.Auth` hosts OpenIddict/Google auth backed by PostgreSQL; startup and routes live in `src/FbApp.Auth/Program.fs`, and client/role seeding is in `Worker.fs`.
- The Elm app entrypoint is `src/FbApp.Web/app/main.ts`, which initializes `src/Main.elm` and bridges random-byte ports through `localStorage` for the OAuth PKCE flow.

## Testing Gotchas

- `FbApp.Auth.IntegrationTests` starts a PostgreSQL Testcontainer, so Docker must be available for that suite.
- `FbApp.Api.IntegrationTests` currently contains only a placeholder passing test; do not treat it as meaningful API coverage.
- Backend apps load config in this order where implemented: `appsettings.json`, environment-specific appsettings, `appsettings.user.json`, then environment variables.

FROM node:lts-alpine AS client-build-stage
WORKDIR /app/src/Client
COPY src/Client/package.json .
COPY src/Client/yarn.lock .
RUN yarn
COPY src/Client .
RUN yarn quasar build -m spa

FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
WORKDIR /build
COPY .config/dotnet-tools.json ./.config/
COPY .paket/Paket.Restore.targets ./.paket/
COPY src/FbApp.Core/FbApp.Core.fsproj ./src/FbApp.Core/
COPY src/FbApp.Domain/FbApp.Domain.fsproj ./src/FbApp.Domain/
COPY src/FbApp.Server/FbApp.Server.fsproj ./src/FbApp.Server/
COPY paket.* ./
RUN dotnet tool restore
RUN dotnet restore ./src/FbApp.Server

COPY src/FbApp.Core/. ./src/FbApp.Core/
COPY src/FbApp.Domain/. ./src/FbApp.Domain/
COPY src/FbApp.Server/. ./src/FbApp.Server/
COPY --from=client-build-stage /app/src/FbApp.Server/wwwroot/. ./src/FbApp.Server/wwwroot/

WORKDIR /build/src/FbApp.Server
RUN dotnet publish -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:5.0-focal AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT [ "dotnet", "FbApp.Server.dll" ]

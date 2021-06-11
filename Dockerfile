FROM node:lts-alpine AS client-build-stage
WORKDIR /app/src/Client
COPY src/Client/package.json .
COPY src/Client/yarn.lock .
RUN yarn
COPY src/Client .
RUN yarn quasar build -m spa

FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
WORKDIR /source
COPY src/FbApp.Core/FbApp.Core.fsproj ./FbApp.Core/
COPY src/FbApp.Domain/FbApp.Domain.fsproj ./FbApp.Domain/
COPY src/FbApp.Server/FbApp.Server.fsproj ./FbApp.Server/
RUN dotnet restore ./FbApp.Server

COPY src/FbApp.Core/. ./FbApp.Core/
COPY src/FbApp.Domain/. ./FbApp.Domain/
COPY src/FbApp.Server/. ./FbApp.Server/
COPY --from=client-build-stage /app/src/FbApp.Server/wwwroot/. ./FbApp.Server/wwwroot/

WORKDIR /source/FbApp.Server
RUN dotnet publish -c release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:5.0-focal AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT [ "dotnet", "FbApp.Server.dll" ]

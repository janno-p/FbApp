FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /build
COPY .config/dotnet-tools.json ./.config/
COPY .paket/Paket.Restore.targets ./.paket/
COPY fbapp/FbApp.Api.fsproj ./fbapp/
COPY paket.* ./
RUN dotnet tool restore
RUN dotnet restore ./fbapp/FbApp.Api.fsproj

COPY fbapp/. ./fbapp/

WORKDIR /build/fbapp
RUN dotnet publish -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT [ "dotnet", "FbApp.Api.dll" ]

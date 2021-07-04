FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
WORKDIR /build
COPY .config/dotnet-tools.json ./.config/
COPY .paket/Paket.Restore.targets ./.paket/
COPY fbapp-api/FbApp.Api.fsproj ./fbapp-api/
COPY paket.* ./
RUN dotnet tool restore
RUN dotnet restore ./fbapp-api/FbApp.Api.fsproj

COPY fbapp-api/. ./fbapp-api/

WORKDIR /build/fbapp-api
RUN dotnet publish -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:5.0-focal AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT [ "dotnet", "FbApp.Api.dll" ]

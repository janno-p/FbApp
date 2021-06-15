FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
WORKDIR /build
COPY .config/dotnet-tools.json ./.config/
COPY .paket/Paket.Restore.targets ./.paket/
COPY fbapp-live-update/FbApp.LiveUpdate.fsproj ./fbapp-live-update/
COPY paket.* ./
RUN dotnet tool restore
RUN dotnet restore ./fbapp-live-update/FbApp.LiveUpdate.fsproj

COPY fbapp-live-update/. ./fbapp-live-update/

WORKDIR /build/fbapp-live-update
RUN dotnet publish -c release -o /app

FROM mcr.microsoft.com/dotnet/runtime:5.0-focal AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT [ "dotnet", "FbApp.LiveUpdate.dll" ]

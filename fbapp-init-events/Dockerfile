FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
WORKDIR /build
COPY .config/dotnet-tools.json ./.config/
COPY .paket/Paket.Restore.targets ./.paket/
COPY fbapp-init-events/FbApp.Init.Events.fsproj ./fbapp-init-events/
COPY paket.* ./
RUN dotnet tool restore
RUN dotnet restore ./fbapp-init-events/FbApp.Init.Events.fsproj

COPY fbapp-init-events/. ./fbapp-init-events/

WORKDIR /build/fbapp-init-events
RUN dotnet publish -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:5.0-focal AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT [ "dotnet", "FbApp.Init.Events.dll" ]

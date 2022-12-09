FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /build
COPY [".config/dotnet-tools.json", ".config/"]
COPY [".paket/Paket.Restore.targets", ".paket/"]
COPY ["src/FbApp.Auth/FbApp.Auth.fsproj", "src/FbApp.Auth/FbApp.Auth.fsproj"]
COPY ["paket.*", "."]
RUN dotnet tool restore
RUN dotnet restore "src/FbApp.Auth/FbApp.Auth.fsproj"
COPY ["src/FbApp.Auth", "src/FbApp.Auth"]
WORKDIR "/build/src/FbApp.Auth"
RUN dotnet build "FbApp.Auth.fsproj" -c Release -o /app/build

FROM build as publish
RUN dotnet publish "FbApp.Auth.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FbApp.Auth.dll"]

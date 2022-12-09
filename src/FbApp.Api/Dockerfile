FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /build
COPY [".config/dotnet-tools.json", ".config/"]
COPY [".paket/Paket.Restore.targets", ".paket/"]
COPY ["src/FbApp.Api/FbApp.Api.fsproj", "src/FbApp.Api/FbApp.Api.fsproj"]
COPY ["paket.*", "."]
RUN dotnet tool restore
RUN dotnet restore "src/FbApp.Api/FbApp.Api.fsproj"
COPY ["src/FbApp.Api", "src/FbApp.Api"]
WORKDIR "/build/src/FbApp.Api"
RUN dotnet build "FbApp.Api.fsproj" -c Release -o /app/build

FROM build as publish
RUN dotnet publish "FbApp.Api.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FbApp.Api.dll"]

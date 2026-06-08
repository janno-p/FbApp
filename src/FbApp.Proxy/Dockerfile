FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build
COPY Directory.Packages.props Directory.Packages.props
COPY ["src/FbApp.Proxy/FbApp.Proxy.fsproj", "src/FbApp.Proxy/FbApp.Proxy.fsproj"]
COPY ["src/FbApp.ServiceDefaults/FbApp.ServiceDefaults.csproj", "src/FbApp.ServiceDefaults/FbApp.ServiceDefaults.csproj"]
RUN dotnet restore "src/FbApp.Proxy/FbApp.Proxy.fsproj"
COPY ["src/FbApp.Proxy", "src/FbApp.Proxy"]
COPY ["src/FbApp.ServiceDefaults", "src/FbApp.ServiceDefaults"]
WORKDIR "/build/src/FbApp.Proxy"
RUN dotnet build "FbApp.Proxy.fsproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FbApp.Proxy.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FbApp.Proxy.dll"]

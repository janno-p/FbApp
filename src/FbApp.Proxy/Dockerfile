FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build
COPY Directory.Packages.props .
COPY ["src/FbApp.Proxy/FbApp.Proxy.fsproj", "src/FbApp.Proxy/FbApp.Proxy.fsproj"]
RUN dotnet restore "src/FbApp.Proxy/FbApp.Proxy.fsproj"
COPY ["src/FbApp.Proxy", "src/FbApp.Proxy"]
WORKDIR "/build/src/FbApp.Proxy"
RUN dotnet build "FbApp.Proxy.fsproj" -c Release -o /app/build

FROM build as publish
RUN dotnet publish "FbApp.Proxy.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FbApp.Proxy.dll"]

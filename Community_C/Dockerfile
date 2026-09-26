# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=8.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Community_C/Community_C.csproj", "Community_C/"]
RUN dotnet restore "Community_C/Community_C.csproj"

COPY ["Community_C/", "Community_C/"]
WORKDIR /src/Community_C
RUN dotnet publish "Community_C.csproj" \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build --chown=app:app /app/publish .

USER app
ENTRYPOINT ["dotnet", "Community_C.dll"]

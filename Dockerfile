# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CompareHub.Backend/CompareHub.Backend.csproj CompareHub.Backend/
RUN dotnet restore CompareHub.Backend/CompareHub.Backend.csproj

COPY CompareHub.Backend/ CompareHub.Backend/
RUN dotnet publish CompareHub.Backend/CompareHub.Backend.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Base image ships Chromium + OS deps preinstalled for this exact Playwright version,
# but only bundles the .NET 8 runtime (this app targets net10.0) — copy the .NET 10
# shared runtime from the SDK build stage in alongside it.
FROM mcr.microsoft.com/playwright/dotnet:v1.54.0-noble AS runtime
WORKDIR /app

COPY --from=build /usr/share/dotnet/shared/Microsoft.NETCore.App /usr/share/dotnet/shared/Microsoft.NETCore.App
COPY --from=build /usr/share/dotnet/shared/Microsoft.AspNetCore.App /usr/share/dotnet/shared/Microsoft.AspNetCore.App

COPY --from=build /app/publish .
RUN pwsh playwright.ps1 install chromium

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "CompareHub.Backend.dll"]

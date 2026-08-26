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

# Base image ships Chromium + OS deps preinstalled for this exact Playwright version.
FROM mcr.microsoft.com/playwright/dotnet:v1.54.0-noble AS runtime
WORKDIR /app

COPY --from=build /app/publish .
RUN pwsh playwright.ps1 install chromium

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "CompareHub.Backend.dll"]

# CompareHub Backend

.NET Web API that lets a user register product source links (e-commerce sites) and scrapes/extracts product data from them for comparison (via HTML selectors, JSON-LD, an API strategy, and Playwright browser automation).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 14+ (a local install, or run one via Docker: `docker run --name comparehub-db -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres`)
- [pwsh](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) (needed once, to install the Playwright browser)

## Setup

1. Confirm Postgres is running and matches the connection string in `CompareHub.Backend/app/Host/API/appsettings.json` (default: `Host=localhost;Port=5432;Database=CompareHub;Username=postgres;Password=postgres`). Create the `CompareHub` database if it doesn't exist yet — the app applies EF Core migrations automatically on startup, so no manual `dotnet ef database update` is needed.
2. Install the Playwright Chromium browser (used by the browser-automation extraction strategy):
   ```
   cd CompareHub.Backend
   dotnet build
   pwsh bin/Debug/net10.0/playwright.ps1 install chromium
   ```
3. Run the API:
   ```
   dotnet run --project CompareHub.Backend/CompareHub.Backend.csproj
   ```
   Available at `http://localhost:5000`. Swagger UI is enabled in development at `http://localhost:5000/swagger`.

## Configuration

All local config lives in `CompareHub.Backend/app/Host/API/appsettings.json` — connection string, JWT issuer/audience/secret, and `Cors:AllowedOrigins` (defaults to `http://localhost:3000` for the frontend dev server). In production these are overridden by environment variables (see `Program.cs`) rather than editing this file.

## Tests

```
dotnet test tests/CompareHub.Backend.Tests/CompareHub.Backend.Tests.csproj
```

## Deployment

Deploys to [Render](https://render.com) as a Docker web service + managed Postgres, defined in `render.yaml` and `Dockerfile` at the repo root. Render is configured to build from the `master` branch — merging `develop` into `master` and pushing triggers an automatic redeploy. See `render.yaml` for the environment variables that need to be set (`Cors__AllowedOrigins__0` should point at the deployed frontend URL).

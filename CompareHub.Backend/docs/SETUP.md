# CompareHub Backend Setup

## Prerequisites
- .NET 8 SDK
- PostgreSQL 14+

## Run
1. Update `app/Host/API/appsettings.json` connection string and JWT key.
2. From `CompareHub.Backend` run:
   - `dotnet restore`
   - `dotnet run --project app/Host/API`

## API Endpoints
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `GET /api/v1/source-links`
- `POST /api/v1/source-links`
- `DELETE /api/v1/source-links/{id}`
- `GET /api/v1/products/search?query=iphone`

## Notes
- Product discovery currently uses `MockProductScraperService`.
- Swap in a real scraper by replacing `IProductScraperService` implementation.

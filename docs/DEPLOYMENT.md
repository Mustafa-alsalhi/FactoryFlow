# FactoryFlow ERP – Production Deployment Guide

## 1. Prerequisites
- .NET 8 Runtime / ASP.NET Core Hosting Bundle
- Microsoft SQL Server 2019+ (or Azure SQL)
- Node.js 20 LTS+ (for building Angular production bundle)
- Reverse Proxy (IIS, Nginx, or Azure App Service / Container Apps) with HTTPS/TLS

## 2. Environment Configuration
Set the following environment variables (never commit production secrets to `appsettings.json`):
- `ConnectionStrings__DefaultConnection`: Production SQL Server connection string with encrypted TLS (`Encrypt=True;TrustServerCertificate=False`).
- `Jwt__Key`: Cryptographically random 64+ character secret key.
- `Jwt__Issuer` / `Jwt__Audience`: Your production API and Frontend URLs.
- `Cors__AllowedOrigins__0`: Your production Angular domain (e.g. `https://erp.yourfactory.com`).

## 3. Database Migration
```bash
dotnet ef database update --project backend/src/FactoryFlow.Infrastructure --startup-project backend/src/FactoryFlow.API
```

## 4. Build & Publish
### Backend
```bash
dotnet publish backend/src/FactoryFlow.API/FactoryFlow.API.csproj -c Release -o ./publish/api
```

### Frontend
```bash
cd frontend
npm ci
npm run build -- --configuration production
```
Serve `frontend/dist/frontend/browser/` via Nginx or CDN with SPA fallback to `index.html`.


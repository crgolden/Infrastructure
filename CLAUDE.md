# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution Structure

The solution (`Infrastructure.slnx`) contains two projects:

- **`Infrastructure/`** — ASP.NET Core 10 web application: real-time dashboard, background health polling, Resend email alerts
- **`Infrastructure.Tests/`** — xUnit v3 unit tests (Moq for mocking)

## What This App Does

Continuously polls 13 services and:
1. Displays a real-time Bootstrap 5 dashboard (SignalR push every 30 s)
2. Sends an alert email via Resend when any service transitions `Healthy → Unhealthy`
3. Sends a recovery email when it transitions back to `Healthy`

**If the Resend client throws at runtime, the exception is intentionally not caught** — it propagates out of `HealthMonitorService.ExecuteAsync`, triggering `BackgroundServiceExceptionBehavior.StopHost` and crashing the process. An app that cannot alert is worse than no app at all; Azure App Service will restart it.

## Architecture

Follows the **onion / clean architecture** model matching the sibling repos:

- **Entries** (Controllers, Hubs, Pages) depend on **Services**
- **Services** depend on **HealthChecks** (via `IHealthCheckService`)
- No circular dependencies

## Services Being Monitored

| Name | Check class | Method |
|---|---|---|
| IIS HTTPS | `IISHttpsHealthCheck` | HTTP GET `:443` |
| IIS HTTP (CertifyTheWeb) | `IISHttpHealthCheck` | HTTP GET `:80` |
| SQL Server | `SqlServerHealthCheck` | `SELECT 1` via `SqlConnection` |
| Elasticsearch | `ElasticsearchHealthCheck` | HTTP GET `/_cluster/health` |
| Kibana | `KibanaHealthCheck` | HTTP GET `/api/status` |
| Plex | `PlexHealthCheck` | HTTP GET `/identity` |
| Yawcam AI | `YawcamHealthCheck` | TCP connect `:5995` |
| Redis | `RedisHealthCheck` | `PING` via `IConnectionMultiplexer` |
| MongoDB | `MongoDbHealthCheck` | `ping` via `IMongoClient` |
| Identity | `IdentityHealthCheck` | HTTP GET `/health`, expects `200 Healthy` |
| Manuals | `ManualsHealthCheck` | HTTP GET `/health`, expects `200 Healthy` |
| Experience | `ExperienceHealthCheck` | HTTP GET `/health`, expects `200 Healthy` |
| Products | `ProductsHealthCheck` | HTTP GET `/health`, expects `200 Healthy` |

## Key Architecture Points

### Dependency Injection Conventions
- All setup in `HostApplicationBuilderExtensions.cs` via the `extension(IHostApplicationBuilder builder)` block syntax
- `AddMonitoringServicesAsync(SecretClient)` — registers health checks, SignalR, alert service, background service
- `AddObservabilityAsync(SecretClient)` — Serilog + Elasticsearch sink
- `AddDataProtection(TokenCredential)` — Azure Blob + Key Vault (identical to Manuals)
- Secrets fetched from Azure Key Vault at startup; `appsettings.json` holds only null-valued non-secret config
- Sibling-app health check URLs (`Identity`, `Manuals`, `Experience`, `Products`) are non-secret and stored directly in `appsettings.json`; unlike other endpoints they also validate response body equals `"Healthy"`

### Health Check Testability
Each `IHealthCheck` accepts its external dependency via constructor injection for easy Moq mocking:
- HTTP checks: `IHttpClientFactory`
- TCP check: `Func<TcpClient>`
- Redis: `IConnectionMultiplexer`
- MongoDB: `IMongoClient`
- SQL Server: `Func<SqlConnection>`

### Background Polling
`HealthMonitorService` extends `BackgroundService` and calls `IHealthCheckService.CheckHealthAsync()` on a configurable interval (`MonitoringOptions:IntervalSeconds`, default 30). After each poll it:
1. Stores the new `HealthSnapshot` in the singleton `IHealthMonitorService`
2. Pushes the snapshot to all SignalR clients via `IHubContext<HealthHub>`
3. Detects status transitions and calls `IAlertService`

### Configuration
Non-secret values → User Secrets (development) or environment variables (production)
Secrets → Azure Key Vault (fetched at startup via `SecretClient`)

See `README.md` for the full configuration reference.

### Observability
- **Serilog** + Elasticsearch sink (`Elastic.Serilog.Sinks` 9.0.0), console sink bootstrap
- **Azure Monitor OpenTelemetry** — metrics and traces
- Health check endpoint at `/health`; health check requests filtered from traces

## Commands

### Build
```bash
dotnet build
dotnet build --configuration Release
```

### Run (development)
```bash
cd Infrastructure
dotnet run
# https://localhost:5001
```

### Test
```bash
# Build first, then run the test binary directly (xunit.v3.mtp-v2 on .NET 10)
dotnet build Infrastructure.Tests
Infrastructure.Tests/bin/Debug/net10.0/Infrastructure.Tests.exe -trait "Category=Unit"

# Or for Release:
dotnet build Infrastructure.Tests -c Release
Infrastructure.Tests/bin/Release/net10.0/Infrastructure.Tests.exe -trait "Category=Unit"
```

### Publish
```bash
dotnet publish Infrastructure -c Release -r win-x86 --self-contained false -o ./publish
```

> `-r win-x86` is required. Azure App Service Free tier only supports 32-bit worker processes.

## Code Quality

`Infrastructure.Tests` sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Fix all warnings before committing.

StyleCop suppressions in `.editorconfig` (same as sibling repos): `SA1000`, `SA1010`, `SA1011`, `SA1101`, `SA1309`, `SA1313`, `SA1413`, `SA1600`, `SA1602`, `SA1633`.

## Deployment

GitHub Actions workflow (`.github/workflows/main_crgolden-infrastructure.yml`):
- **Build job**: build → unit tests with coverage → SonarCloud analysis → publish artifact
- **Deploy job**: download artifact → Azure OIDC login → deploy to Azure App Service `crgolden-infrastructure`

**Firewall note:** Windows Firewall inbound rules for monitored ports must allow Azure App Service outbound IPs. Rules currently scoped to specific IPs or the local subnet will need updating.

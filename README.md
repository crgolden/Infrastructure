# Infrastructure

[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-infrastructure](https://github.com/crgolden/Infrastructure/actions/workflows/main_crgolden-infrastructure.yml/badge.svg)](https://github.com/crgolden/Infrastructure/actions/workflows/main_crgolden-infrastructure.yml)

[![Quality gate](https://sonarcloud.io/api/project_badges/quality_gate?project=crgolden_Infrastructure)](https://sonarcloud.io/summary/new_code?id=crgolden_Infrastructure)

An ASP.NET Core 10 service-health monitoring application that continuously polls critical infrastructure services, displays a real-time dashboard, and emails alerts on status changes.

How the parts fit together and the rules each keeps: [ARCHITECTURE.md](ARCHITECTURE.md). The dashboard's design: [DESIGN.md](DESIGN.md). How to run the tests: [TESTING.md](TESTING.md). Guidance for coding agents working in this repo: [AGENTS.md](AGENTS.md).

## Services Monitored

Every check is registered in `Program.cs` and runs each poll cycle. HTTP checks GET the URL from the listed config key; TCP checks open a socket to the host/port; the rest use their native client.

| Service | Check | Configured via |
|---|---|---|
| IIS HTTPS | HTTP GET | `ServiceEndpointOptions:IisHttps` |
| SQL Server | `SELECT 1` via `SqlConnection` | `SqlConnectionStringBuilder` |
| PostgreSQL | `SELECT 1` via `NpgsqlConnection` | `NpgsqlConnectionStringBuilder` |
| Elasticsearch | HTTP GET `/_cluster/health` (Basic auth) | `ServiceEndpointOptions:Elasticsearch` |
| Kibana | HTTP GET `/api/status` (Basic auth) | `ServiceEndpointOptions:Kibana` |
| Plex Media Server | HTTP GET `/identity` | `ServiceEndpointOptions:Plex` |
| Home Assistant | HTTP GET | `ServiceEndpointOptions:HomeAssistant` |
| Uptime Kuma | HTTP GET | `ServiceEndpointOptions:UptimeKuma` |
| Grafana | HTTP GET `/api/health` | `ServiceEndpointOptions:Grafana` |
| Grafana Alloy | TCP connect | `ServiceEndpointOptions:AlloyHost` / `:AlloyPort` |
| Yawcam AI | TCP connect | `ServiceEndpointOptions:YawcamHost` / `:YawcamPort` |
| WMSvc | TCP connect | `ServiceEndpointOptions:WmsvcHost` / `:WmsvcPort` |
| Redis | `PING` via `IConnectionMultiplexer` | `RedisHost` / `RedisPort` / `RedisSsl` |
| MongoDB | `ping` command via `IMongoClient` | `MongoServerHost` / `MongoServerPort` / `MongoUseTls` |
| Identity (the OIDC provider) | HTTP GET `/health`, body `Healthy` | `OidcAuthority` |
| Manuals | HTTP GET `/health`, body `Healthy` | `ManualsApiAddress` |
| Inventory | HTTP GET `/health`, body `Healthy` | `InventoryServerAddress` |
| Products | HTTP GET `/health`, body `Healthy` | `ProductsApiAddress` |
| Churches | HTTP GET `/health`, body `Healthy` | `ChurchesServerAddress` |
| Directory | HTTP GET `/health`, body `Healthy` | `DirectoryApiAddress` |
| Curator | HTTP GET `/health`, body `Healthy` | `CuratorApiAddress` |
| Librarian | HTTP GET `/health`, body `Healthy` | `LibrarianServerAddress` |

The last eight are web apps; each check extends `SiblingAppHealthCheck`, which reads the app's base URL from the listed key and reports healthy only when the `/health` body is exactly `Healthy`. Point any key at the app you want to watch.

Health checks are polled every `MonitoringOptions:IntervalSeconds` (default 30). When a service transitions from `Healthy` (or `Unknown`) to `Unhealthy`, an alert message is published to the Azure Service Bus `email` queue; a recovery message is sent when it returns to `Healthy`. `Degraded` does not trigger an email.

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 |
| Real-time dashboard | SignalR |
| Email alerts | Azure Service Bus |
| Observability | OpenTelemetry (OTLP → Grafana Alloy), Serilog → Elasticsearch |
| Hosting | Azure App Service |
| Secrets | Azure Key Vault |
| Data Protection | Azure Blob Storage + Azure Key Vault |

## Prerequisites

| Tool | Notes |
|---|---|
| .NET 10 SDK | `global.json` pins `10.0.201` and rolls forward to the latest feature band |
| GitHub personal access token | `read:packages` scope: the `Shared` package restores from the `crgolden` GitHub Packages feed (`NuGet.Config`), which asks for a token even for a public package. Store it as the `GitHub` source's credentials in your user NuGet configuration (`dotnet nuget update source GitHub --username <user> --password <token> --store-password-in-clear-text --configfile "$env:APPDATA\NuGet\NuGet.Config"`), never in this repo |
| An OIDC provider | The dashboard signs in with OpenID Connect (`OidcAuthority`, `InfrastructureClientId`, `InfrastructureClientSecret`) |
| Azure Key Vault | Backs the secrets listed below in production, as Key Vault-referenced App Service settings the platform resolves into configuration at startup — non-production reads the same config keys from User Secrets / environment variables |
| Azure Service Bus namespace | With an `email` queue for outbound alert messages |

## Clone and build

```powershell
git clone https://github.com/crgolden/Infrastructure.git
cd Infrastructure
dotnet tool restore
dotnet build Infrastructure.slnx
```

## Getting Started

### 1. Configure User Secrets

All `null` values in `appsettings.json` must be supplied via **User Secrets** (development), environment variables (CI), or Key Vault-referenced App Service settings (production) — the app reads every one of them the same way, via `IConfiguration.GetRequired<T>()`, regardless of environment. In non-production, `DefaultAzureCredential` is never constructed — all config comes from User Secrets or env vars.

**Non-secret values (User Secrets / environment / `appsettings.json`):**

| Key | Description |
|---|---|
| `OidcAuthority` | Identity OIDC authority — also the Identity `/health` target |
| `MonitoringOptions:IntervalSeconds` | Poll interval in seconds (default 30) |
| `TelemetryOptions:HealthMonitorFailureDescription` | Description of the `infrastructure.health_monitor.failures` counter |
| `ServiceEndpointOptions:IisHttps` | URL for IIS HTTPS check |
| `ServiceEndpointOptions:Elasticsearch` | URL for Elasticsearch health check |
| `ServiceEndpointOptions:Kibana` | URL for Kibana status check |
| `ServiceEndpointOptions:Plex` | URL for Plex identity check |
| `ServiceEndpointOptions:HomeAssistant` | URL for Home Assistant check |
| `ServiceEndpointOptions:UptimeKuma` | URL for Uptime Kuma check |
| `ServiceEndpointOptions:Grafana` | URL for Grafana health check |
| `ServiceEndpointOptions:AlloyHost` / `:AlloyPort` | Host/port for the Grafana Alloy TCP check |
| `ServiceEndpointOptions:YawcamHost` / `:YawcamPort` | Host/port for the Yawcam TCP check |
| `ServiceEndpointOptions:WmsvcHost` / `:WmsvcPort` | Host/port for the WMSvc TCP check |
| `SqlConnectionStringBuilder:DataSource` | SQL Server host |
| `SqlConnectionStringBuilder:InitialCatalog` | Database name (default `master`) |
| `NpgsqlConnectionStringBuilder:Host` | PostgreSQL host |
| `NpgsqlConnectionStringBuilder:Database` | PostgreSQL database name (default `curator`) |
| `RedisHost` / `RedisPort` / `RedisSsl` | Redis endpoint and TLS flag |
| `MongoDatabaseName` | MongoDB auth database (default `crgolden`) |
| `MongoServerHost` / `MongoServerPort` / `MongoUseTls` | MongoDB endpoint and TLS flag |
| `InventoryServerAddress` | Inventory base URL (`/health` target) |
| `ManualsApiAddress` | Manuals base URL (`/health` target) |
| `ProductsApiAddress` | Products base URL (`/health` target) |
| `ChurchesServerAddress` | Churches base URL (`/health` target) |
| `DirectoryApiAddress` | Directory base URL (`/health` target) |
| `CuratorApiAddress` | Curator base URL (`/health` target) |
| `LibrarianServerAddress` | Librarian base URL (`/health` target) |

> The alert recipient (`AlertOptions.RecipientEmail`) is **not** a config key — it is bound from the `Email1` secret at startup.

**Production-only configuration (Azure App Service settings):**

| Key | Description |
|---|---|
| `ElasticsearchNode` | Elasticsearch node URI (Serilog sink) |
| `BlobUri` | Azure Blob Storage URI for Data Protection keys |
| `DataProtectionKeyIdentifier` | Azure Key Vault key URI for Data Protection |
| `ServiceBusNamespace` | Service Bus fully-qualified namespace (email queue) |
| `AlloyEndpoint` | OTLP exporter endpoint for metrics + traces |
| `WEBSITE_SITE_NAME` | App name (set by Azure; used as the OpenTelemetry service name) |
| `WEBSITE_HOSTNAME` | Host (set by Azure; used by `KeepaliveService` to self-ping `/ping`) |
| `DefaultAzureCredentialOptions` | `DefaultAzureCredential` chain options |

**Secrets:** in production, every one of these is an App Service setting holding a `@Microsoft.KeyVault(SecretUri=...)` reference that the platform resolves into configuration at startup under the same key name; non-production reads them from **User Secrets / environment variables**. There is no code-level distinction between the two — both are plain `IConfiguration` reads.

| Key | Description |
|---|---|
| `ElasticsearchUsername` | Elasticsearch user (Serilog sink + ES/Kibana checks) |
| `ElasticsearchPassword` | Elasticsearch password |
| `SqlConnectionStringBuilder:UserID` | SQL Server login |
| `SqlConnectionStringBuilder:Password` | SQL Server password |
| `RedisPassword` | Redis `AUTH` password |
| `MongoDbUsername` | MongoDB username |
| `MongoDbPassword` | MongoDB password |
| `PostgreSqlUserId` | PostgreSQL login |
| `PostgreSqlPassword` | PostgreSQL password |
| `Email1` | Alert recipient email address |
| `InfrastructureClientId` | OIDC client ID |
| `InfrastructureClientSecret` | OIDC client secret |
| `ServiceBusConnectionString` | Service Bus connection string (non-production only) |

### 2. Run

```bash
cd Infrastructure
dotnet run
```

| Endpoint | URL | Notes |
|---|---|---|
| Dashboard | `https://localhost:5001/` | Razor page; requires OIDC login |
| JSON status API | `https://localhost:5001/api/status` | Latest `HealthSnapshot`; requires auth; `503` until the first poll completes |
| SignalR hub | `https://localhost:5001/hubs/health` | Pushes the `ReceiveSnapshot` message; requires auth |
| ASP.NET Core health endpoint | `https://localhost:5001/health` | Anonymous; liveness of *this app only* — body `Healthy`. The monitored services are reported through the dashboard, `/api/status`, and the hub, not here |
| Keepalive ping | `https://localhost:5001/ping` | Anonymous; returns 200. `KeepaliveService` self-pings this in Azure to avoid cold starts |

## Project Structure

```
Infrastructure/        # ASP.NET Core 10 — health polling, SignalR dashboard, Azure Service Bus email alerts
Infrastructure.Tests.Unit/  # xUnit v3 unit tests (Moq)
```

Key components inside `Infrastructure/`:

- `HealthChecks/` — one `IHealthCheck` per monitored service; the eight sibling-app checks extend `SiblingAppHealthCheck`.
- `Services/HealthMonitorService` — `BackgroundService` poll loop that stores the latest snapshot, broadcasts it over SignalR, and triggers alerts on status transitions.
- `Services/AlertService` — publishes alert/recovery emails to the Azure Service Bus `email` queue.
- `Services/KeepaliveService` — `BackgroundService` that self-pings `/ping` every 10 minutes when `WEBSITE_HOSTNAME` is set.
- `Controllers/StatusController` + `Hubs/HealthHub` — the `[Authorize]` status API and SignalR hub.

## Commands

```bash
# Build
dotnet build

# Unit tests only
dotnet test --project Infrastructure.Tests.Unit --configuration Release -- --filter-trait "Category=Unit"

# Publish web app (-r win-x86 required: Azure App Service Free tier supports 32-bit only)
dotnet publish Infrastructure -c Release -r win-x86 --self-contained false -o ./publish
```

The unit tier needs no network, service or secret; [TESTING.md](TESTING.md) covers it in full.

## Gate

`gate.ps1` is the maintainers' gate. It loads a private toolkit from a folder beside this repo, so in a plain clone it stops at once with `GATE: FAILED`. To gate your own change, run the checks CI runs, in this order:

```powershell
dotnet tool restore
dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
dotnet jb inspectcode Infrastructure.slnx --no-build -e=WARNING --output=inspectcode.sarif
dotnet test --project Infrastructure.Tests.Unit --no-build --configuration Release -- --filter-trait "Category=Unit"
```

`jb inspectcode` passes only when `inspectcode.sarif` holds no results.

## Deployment

The GitHub Actions workflow triggers on pushes to `main` and pull requests.

**Build job** — runs on every trigger:
1. Builds the solution (`dotnet build --configuration Release`)
2. Runs unit tests with coverage
3. Runs SonarCloud analysis, publishes the web app, and uploads the artifact

**Deploy job** — runs after a successful build on `main`:
1. Deploys the web app to **Azure App Service** `crgolden-infrastructure` (Production slot) via Azure OIDC

> **Firewall note:** Every monitored service listens on a host and port supplied through the configuration keys above, and each of those must allow inbound traffic from the deployed app's outbound IP addresses. Any firewall rule scoped to specific IPs or to the local subnet needs updating when those outbound addresses change — a monitored service that is up but unreachable reports `Unhealthy` and triggers an alert exactly as a real outage would.

# Infrastructure

[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-infrastructure](https://github.com/crgolden/Infrastructure/actions/workflows/main_crgolden-infrastructure.yml/badge.svg)](https://github.com/crgolden/Infrastructure/actions/workflows/main_crgolden-infrastructure.yml)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=crgolden_Infrastructure&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=crgolden_Infrastructure)

An ASP.NET Core 10 service-health monitoring application that continuously polls critical infrastructure services, displays a real-time dashboard, and emails alerts on status changes.

## Sibling Applications

Infrastructure is the **observability surface** for a five-app system. It polls each sibling's `/health` endpoint and sends an alert email on the first `Healthy → Unhealthy` transition (and a recovery email on the way back).

| Repo | Role | How Infrastructure interacts |
|---|---|---|
| [Identity](https://github.com/crgolden/Identity) | OIDC Identity Provider | `GET /health` — expects `200 Healthy` |
| [Experience](https://github.com/crgolden/Experience) | Angular SPA + ASP.NET Core BFF | `GET /health` — expects `200 Healthy` |
| [Manuals](https://github.com/crgolden/Manuals) | Azure OpenAI chat API | `GET /health` — expects `200 Healthy` |
| [Products](https://github.com/crgolden/Products) | OData v4 product catalog API | `GET /health` — expects `200 Healthy` |

## Services Monitored

| Service | Port | Check Method |
|---|---|---|
| IIS HTTPS | 443 | HTTP GET |
| IIS HTTP (CertifyTheWeb) | 80 | HTTP GET |
| SQL Server | 1433 | `SELECT 1` via `SqlConnection` |
| Elasticsearch | 9200 | HTTP GET `/_cluster/health` |
| Kibana | 5601 | HTTP GET `/api/status` |
| Plex Media Server | 32400 | HTTP GET `/identity` |
| Yawcam AI | 5995 | TCP connect |
| Redis | 6379 | `PING` via `IConnectionMultiplexer` |
| MongoDB | 27017 | `ping` command via `IMongoClient` |
| [Identity](https://github.com/crgolden/Identity) | 443 | HTTP GET `/health`, expects `200 Healthy` |
| [Manuals](https://github.com/crgolden/Manuals) | 443 | HTTP GET `/health`, expects `200 Healthy` |
| [Experience](https://github.com/crgolden/Experience) | 443 | HTTP GET `/health`, expects `200 Healthy` |
| [Products](https://github.com/crgolden/Products) | 443 | HTTP GET `/health`, expects `200 Healthy` |

Health checks are polled every 30 seconds (configurable). When a service transitions from `Healthy` to `Unhealthy`, an alert email is sent via Resend. A recovery email is sent when the service returns to `Healthy`.

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 |
| Real-time dashboard | SignalR |
| Email alerts | Resend |
| Observability | Azure Monitor, OpenTelemetry, Serilog, Elasticsearch |
| Hosting | Azure App Service |
| Secrets | Azure Key Vault |
| Data Protection | Azure Blob Storage + Azure Key Vault |

## Prerequisites

| Tool | Notes |
|---|---|
| .NET 10 SDK | |
| Azure CLI | `az login` for local dev (used by `DefaultAzureCredential`) |
| Azure Key Vault | With the secrets listed below |
| Resend account | `noreply@crgolden.com` verified as a sender domain |

## Getting Started

### 1. Configure User Secrets

All `null` values in `appsettings.json` must be supplied via **User Secrets** (development) or environment variables / Azure App Configuration (production). Secret values are fetched from **Azure Key Vault** at startup.

**Non-secret values (User Secrets / environment):**

| Key | Description |
|---|---|
| `KeyVaultUri` | Azure Key Vault URI |
| `ElasticsearchNode` | Elasticsearch node URI (for Serilog sink) |
| `BlobUri` | Azure Blob Storage URI for Data Protection keys |
| `DataProtectionKeyIdentifier` | Azure Key Vault key URI for Data Protection |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure Monitor connection string |
| `AlertOptions:RecipientEmail` | Email address to receive health alerts |
| `ServiceEndpoints:IisHttp` | URL for IIS HTTP check |
| `ServiceEndpoints:IisHttps` | URL for IIS HTTPS check |
| `ServiceEndpoints:Elasticsearch` | URL for Elasticsearch health check |
| `ServiceEndpoints:Kibana` | URL for Kibana status check |
| `ServiceEndpoints:Plex` | URL for Plex identity check |
| `ServiceEndpoints:YawcamHost` | Hostname for Yawcam TCP check |
| `ServiceEndpoints:YawcamPort` | Port for Yawcam TCP check (default: 5995) |
| `SqlConnectionStringBuilder:DataSource` | SQL Server host |
| `SqlConnectionStringBuilder:InitialCatalog` | Database name |
| `RedisHost` | Redis hostname |
| `RedisPort` | Redis port |
| `MongoDbHost` | MongoDB hostname |
| `MongoDbPort` | MongoDB port |

**Secrets (Azure Key Vault):**

| Secret name | Description |
|---|---|
| `ResendApiKey` | Resend API key for email alerts |
| `ElasticsearchUsername` | Elasticsearch username (Serilog sink) |
| `ElasticsearchPassword` | Elasticsearch password (Serilog sink) |
| `SqlServerUserId` | SQL Server login |
| `SqlServerPassword` | SQL Server password |
| `RedisPassword` | Redis `AUTH` password |
| `MongoDbUsername` | MongoDB username |
| `MongoDbPassword` | MongoDB password |

### 2. Run

```bash
cd Infrastructure
dotnet run
```

| Endpoint | URL |
|---|---|
| Dashboard | `https://localhost:5001/` |
| JSON status API | `https://localhost:5001/api/status` |
| ASP.NET Core health endpoint | `https://localhost:5001/health` |

## Project Structure

```
Infrastructure/        # ASP.NET Core 10 — health polling, SignalR dashboard, Resend email alerts
Infrastructure.Tests/  # xUnit v3 unit tests (Moq)
```

## Commands

```bash
# Build
dotnet build

# Unit tests only
dotnet test --project Infrastructure.Tests --configuration Release -- --filter-trait "Category=Unit"

# Publish web app (-r win-x86 required: Azure App Service Free tier supports 32-bit only)
dotnet publish Infrastructure -c Release -r win-x86 --self-contained false -o ./publish
```

## Deployment

The GitHub Actions workflow triggers on pushes to `main` and pull requests.

**Build job** — runs on every trigger:
1. Builds the solution (`dotnet build --configuration Release`)
2. Runs unit tests with coverage
3. Runs SonarCloud analysis, publishes the web app, and uploads the artifact

**Deploy job** — runs after a successful build on `main`:
1. Deploys the web app to **Azure App Service** `crgolden-infrastructure` (Production slot) via Azure OIDC

> **Firewall note:** Windows Firewall inbound rules for monitored ports (1433, 9200, 5601, 32400, 5995, 6379, 27017) must allow inbound traffic from Azure App Service outbound IPs. Update any rules scoped to specific IPs or the local subnet accordingly.

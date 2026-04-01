# Infrastructure

An ASP.NET Core 10 service-health monitoring application that continuously polls critical infrastructure services, displays a real-time dashboard, and emails alerts on status changes.

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

Health checks are polled every 30 seconds (configurable). When a service transitions from `Healthy` to `Unhealthy`, an alert email is sent via Resend. A recovery email is sent when the service returns to `Healthy`.

## Prerequisites

- .NET 10 SDK
- Azure CLI (`az login`) for local development (used by `DefaultAzureCredential`)
- Azure Key Vault with the secrets listed below
- A Resend account with `noreply@crgolden.com` verified as a sender domain

## Configuration

All `null` values in `appsettings.json` must be supplied via **User Secrets** (development) or environment variables / Azure App Configuration (production). Secret values are fetched from **Azure Key Vault** at startup.

### Non-secret values (User Secrets / environment)

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

### Secrets (Azure Key Vault)

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

## Build & Run

```bash
# Build
dotnet build

# Run (development — requires User Secrets and az login)
cd Infrastructure
dotnet run

# Dashboard
https://localhost:5001/

# JSON status API
https://localhost:5001/api/status

# ASP.NET Core health endpoint
https://localhost:5001/health

# Test
dotnet test --project Infrastructure.Tests -- --filter-trait "Category=Unit"
```

## Deployment

Deployed to Azure App Service via GitHub Actions. Health checks connect to services via the external hostname (`deeprog.servehttp.com`) and router-forwarded ports.

```bash
dotnet publish Infrastructure -c Release -r win-x86 --self-contained false -o ./publish
```

> **Firewall note:** Windows Firewall inbound rules for monitored ports (1433, 9200, 5601, 32400, 5995, 6379, 27017) must allow inbound traffic from Azure App Service outbound IPs. Update any rules scoped to specific IPs or the local subnet accordingly.

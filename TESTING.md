# Testing

The Infrastructure test suite uses xUnit v3 and covers a single tier: **unit tests** that run on every push with no external dependencies.

## Test tier

| Tier | Trait | Project | Requires Azure? | Runs in CI |
|------|-------|---------|-----------------|------------|
| Unit | `Category=Unit` | `Infrastructure.Tests` | No | Every push/PR |

---

## Running tests locally

```bash
# Build first (required — xunit.v3.mtp-v2 on .NET 10 uses the compiled exe directly)
dotnet build Infrastructure.Tests

# Run unit tests (Debug build)
Infrastructure.Tests/bin/Debug/net10.0/Infrastructure.Tests.exe -trait "Category=Unit"

# Or for Release build:
dotnet build Infrastructure.Tests -c Release
Infrastructure.Tests/bin/Release/net10.0/Infrastructure.Tests.exe -trait "Category=Unit"
```

> **Why run the `.exe` directly?** `Infrastructure.Tests` uses `xunit.v3.mtp-v2` (Microsoft Testing Platform) on .NET 10. There is no VSTest adapter for this combination — `dotnet test` will not discover the tests. The compiled executable is the correct entry point.

---

## Test coverage

### `HealthChecks/`

One test class per health check. Each check accepts its external dependency via constructor injection so the class under test is instantiated directly with a Moq mock — no application startup, no Azure, no real services.

| Class | Dependency mocked | What it tests |
|-------|-------------------|---------------|
| `ElasticsearchHealthCheckTests` | `IHttpClientFactory` | `GET /_cluster/health` — healthy / unhealthy response |
| `ExperienceHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `IdentityHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `IISHttpHealthCheckTests` | `IHttpClientFactory` | HTTP `:80` reachability |
| `IISHttpsHealthCheckTests` | `IHttpClientFactory` | HTTPS `:443` reachability |
| `KibanaHealthCheckTests` | `IHttpClientFactory` | `GET /api/status` |
| `ManualsHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `MongoDbHealthCheckTests` | `IMongoClient` | `ping` command |
| `PlexHealthCheckTests` | `IHttpClientFactory` | `GET /identity` |
| `RedisHealthCheckTests` | `IConnectionMultiplexer` | `PING` command |
| `SqlServerHealthCheckTests` | `Func<SqlConnection>` | `SELECT 1` |
| `YawcamHealthCheckTests` | `Func<TcpClient>` | TCP connect `:5995` |

### `Hubs/`

| Class | What it tests |
|-------|---------------|
| `HealthHubTests` | `HealthHub` — SignalR hub method that broadcasts `HealthSnapshot` to all clients |

### `Services/`

| Class | What it tests |
|-------|---------------|
| `AlertServiceTests` | `AlertService` — email alert on `Healthy → Unhealthy` / `Unhealthy → Healthy` transitions via mocked `IEmailSender` |
| `HealthMonitorServiceTests` | `HealthMonitorService` — background poll loop: snapshot storage, SignalR broadcast, alert triggering |

### `Controllers/`

| Class | What it tests |
|-------|---------------|
| `StatusControllerTests` | `StatusController` — returns current `HealthSnapshot` from `IHealthMonitorService` |

---

## CI pipeline

The GitHub Actions workflow (`.github/workflows/main_crgolden-infrastructure.yml`) runs on every push and PR:

1. Build solution (`dotnet build --no-incremental --configuration Release`)
2. Run unit tests with coverage
3. SonarCloud analysis
4. Publish artifact → deploy to Azure App Service `crgolden-infrastructure`

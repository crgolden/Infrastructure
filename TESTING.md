# Testing

The Infrastructure test suite uses xUnit v3 and covers a single tier: **unit tests** that run on every push with no external dependencies.

Unit test coding standards (MockBehavior.Strict, argument verification, SetupSequence, no control-flow in tests, etc.) are in the workspace-level [Unit Test Standards](../TESTING.md#unit-test-standards).

## Test tier

| Tier | Trait | Project | Requires Azure? | Runs in CI |
|------|-------|---------|-----------------|------------|
| Unit | `Category=Unit` | `Infrastructure.Tests` | No | Every push/PR |

---

For the `.NET 10 SDK xUnit caveat` (why `dotnet test` doesn't work), and why you run the compiled `.exe` directly, see the workspace-level [TESTING.md](../TESTING.md).

## Running Tests Locally

No Azure credentials required — all tests are unit tests.

```powershell
dotnet build Infrastructure.Tests --configuration Debug
.\Infrastructure.Tests\bin\Debug\net10.0\Infrastructure.Tests.exe -trait "Category=Unit" -showLiveOutput
```

---

## Test coverage

### `HealthChecks/`

One test class per health check. Each check accepts its external dependency via constructor injection so the class under test is instantiated directly with a Moq mock — no application startup, no Azure, no real services.

| Class | Dependency mocked | What it tests |
|-------|-------------------|---------------|
| `ElasticsearchHealthCheckTests` | `IHttpClientFactory` | `GET /_cluster/health` — healthy / unhealthy response |
| `ExperienceHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `IdentityHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `IISHttpsHealthCheckTests` | `IHttpClientFactory` | HTTPS `:443` reachability |
| `KibanaHealthCheckTests` | `IHttpClientFactory` | `GET /api/status` |
| `ManualsHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `MongoDbHealthCheckTests` | `IMongoClient` | `ping` command |
| `PlexHealthCheckTests` | `IHttpClientFactory` | `GET /identity` |
| `ProductsHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `RedisHealthCheckTests` | `IConnectionMultiplexer` | `PING` command |
| `SqlServerHealthCheckTests` | `Func<SqlConnection>` | `SELECT 1` |
| `YawcamHealthCheckTests` | `Func<TcpClient>` | TCP connect `:5995` |
| `WMSvcHealthCheckTests` | `Func<TcpClient>` | TCP connect `:8172` |

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

---

## Local SonarCloud analysis

Generate coverage first (unit tests only), then run from `Infrastructure/`:

```powershell
$env:SONAR_TOKEN = "<token>"
& "$env:SystemDrive\sonar-scanner-8.0.1.6346-windows-x64\bin\sonar-scanner.bat" `
  "-Dsonar.projectKey=crgolden_Infrastructure" `
  "-Dsonar.organization=crgolden" `
  "-Dsonar.sources=Infrastructure" `
  "-Dsonar.tests=Infrastructure.Tests" `
  "-Dsonar.exclusions=**/bin/**,**/obj/**" `
  "-Dsonar.cs.vscoveragexml.reportsPaths=coverage.xml"
```

Required coverage files: `coverage.xml`.

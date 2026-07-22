# Testing

The Infrastructure test suite uses xUnit v3 and covers a single tier: **unit tests** that run on every push with no external dependencies.

Unit test coding standards (MockBehavior.Strict, argument verification, SetupSequence, no control-flow in tests, etc.) are in the workspace-level [Unit Test Standards](../TESTING.md#unit-test-standards).

## Test tier

| Tier | Trait | Project | Requires Azure? | Runs in CI |
|------|-------|---------|-----------------|------------|
| Unit | `Category=Unit` | `Infrastructure.Tests.Unit` | No | Every push/PR |

---

`Infrastructure.Tests.Unit` sets `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>`, so both `dotnet test` (used by CI) and the compiled `.exe` (preferred locally for `-showLiveOutput`) route through the xUnit v3 Microsoft Testing Platform runner. See the workspace-level [TESTING.md](../TESTING.md) for the runner-flag caveats and the coverage rationale.

## Running Tests Locally

No Azure credentials required — all tests are unit tests.

```powershell
dotnet build Infrastructure.Tests.Unit --configuration Debug
.\Infrastructure.Tests.Unit\bin\Debug\net10.0\Infrastructure.Tests.Unit.exe -trait "Category=Unit" -showLiveOutput
```

---

## Test coverage

### `HealthChecks/`

One test class per health check. Each check accepts its external dependency via constructor injection so the class under test is instantiated directly with a Moq mock — no application startup, no Azure, no real services.

| Class | Dependency mocked | What it tests |
|-------|-------------------|---------------|
| `ElasticsearchHealthCheckTests` | `IHttpClientFactory` | `GET /_cluster/health` — healthy / unhealthy response |
| `InventoryHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `IdentityHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `IISHttpsHealthCheckTests` | `IHttpClientFactory` | HTTPS `:443` reachability |
| `KibanaHealthCheckTests` | `IHttpClientFactory` | `GET /api/status` |
| `ManualsHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `MongoDbHealthCheckTests` | `IMongoClient` | `ping` command |
| `PlexHealthCheckTests` | `IHttpClientFactory` | `GET /identity` |
| `ProductsHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `ChurchesHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `DirectoryHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `CuratorHealthCheckTests` | `IHttpClientFactory` | `GET /health` response body == `"Healthy"` |
| `RedisHealthCheckTests` | `IConnectionMultiplexer` | `PING` command |
| `SqlServerHealthCheckTests` | `Func<SqlConnection>` | `SELECT 1` |
| `YawcamHealthCheckTests` | `Func<TcpClient>` | TCP connect `:5995` |
| `WMSvcHealthCheckTests` | `Func<TcpClient>` | TCP connect `:8172` |
| `KafkaHealthCheckTests` | `Func<TcpClient>` | TCP connect `:9093` |
| `HomeAssistantHealthCheckTests` | `IHttpClientFactory` | `GET /` → HTTP 200 |
| `UptimeKumaHealthCheckTests` | `IHttpClientFactory` | `GET /` → HTTP 200 |
| `GrafanaHealthCheckTests` | `IHttpClientFactory` | `GET /api/health` → HTTP 200 |
| `AlloyHealthCheckTests` | `Func<TcpClient>` | TCP connect to `AlloyHost:AlloyPort` |

### `Hubs/`

| Class | What it tests |
|-------|---------------|
| `HealthHubTests` | `HealthHub` — is a SignalR `Hub` (no methods yet) |

### `Pages/`

| Class | What it tests |
|-------|---------------|
| `LogoutTests` | `LogoutModel.OnPost` — returns `SignOutResult` with both Cookie and OIDC schemes, redirect URI `"/"` |

### `Services/`

| Class | What it tests |
|-------|---------------|
| `AlertServiceTests` | `AlertService` — publishes alert/recovery `ServiceBusMessage`s to the `email` queue via mocked `IAzureClientFactory<ServiceBusClient>` / `ServiceBusSender` |
| `HealthMonitorServiceTests` | `HealthMonitorService` — background poll loop: snapshot storage, SignalR broadcast, alert triggering; resilience: poll continues when `CheckHealthAsync` throws, SignalR throw does not block alerting, alert throw does not corrupt transition state |
| `KeepaliveServiceTests` | `KeepaliveService` — no HTTP call when `WEBSITE_HOSTNAME` is unset; self-pings `/ping` when it is set |

### `Controllers/`

| Class | What it tests |
|-------|---------------|
| `StatusControllerTests` | `StatusController` — returns the current `HealthSnapshot` from `IHealthMonitorService`, or `503` when none yet |

---

## CI pipeline

The GitHub Actions workflow (`.github/workflows/main_crgolden-infrastructure.yml`) runs on every push and PR:

1. Build solution (`dotnet build --no-incremental --configuration Release`)
2. Run unit tests with coverage
3. SonarCloud analysis
4. Publish artifact → deploy to Azure App Service `crgolden-infrastructure`

---

## Local SonarCloud analysis

Generate coverage first, then run from `Infrastructure/`. Unit coverage is OpenCover (branch-bearing,
via `coverlet.console` pinned in `dotnet-tools.json` — restore with `dotnet tool restore`; see the
workspace `TESTING.md` for the rationale). Infrastructure is unit-only in CI, so OpenCover is the only report.

```powershell
dotnet build Infrastructure.Tests.Unit --configuration Release
dotnet tool restore
dotnet coverlet Infrastructure.Tests.Unit\bin\Release\net10.0 `
  --target "dotnet" `
  --targetargs "test --project Infrastructure.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit" `
  --format opencover --output "coverage.opencover.xml" `
  --skipautoprops --exclude-by-attribute GeneratedCodeAttribute `
  --exclude-by-file "**/obj/**" --exclude-by-file "**/Program.cs" `
  --does-not-return-attribute DoesNotReturnAttribute --include "[Infrastructure]*"

$env:SONAR_TOKEN = "<token>"
& "$env:SystemDrive\sonar-scanner-8.0.1.6346-windows-x64\bin\sonar-scanner.bat" `
  "-Dsonar.projectKey=crgolden_Infrastructure" `
  "-Dsonar.organization=crgolden" `
  "-Dsonar.sources=Infrastructure" `
  "-Dsonar.tests=Infrastructure.Tests.Unit" `
  "-Dsonar.exclusions=**/bin/**,**/obj/**" `
  "-Dsonar.cs.opencover.reportsPaths=coverage.opencover.xml"
```

Required coverage files: `coverage.opencover.xml` (unit, OpenCover).

### When to build a truth table

The coverage **score is read from SonarCloud, never hand-maintained** here. Build a per-method table in `COVERAGE-TRUTH-TABLES.md` only when SonarCloud flags a method with **cognitive complexity > 15 AND uncovered conditions > 0**: the table is escalation for the gnarly few, not a per-class deliverable. See `../DESIGN-LANGUAGE.md` and `../TESTING-COVERAGE.md`.

# Testing

The Infrastructure test suite uses xUnit v3 and covers a single tier: **unit tests** that run on every push with no external dependencies.

## Unit test standards

These apply to every `[Fact]` and `[Theory]` in `Infrastructure.Tests.Unit`.

- **One `sealed` test class per SUT**, file-scoped namespace, `[Trait("Category", "Unit")]` on the class.
- **`MockBehavior.Strict` is required** — every mock is `new Mock<T>(MockBehavior.Strict)`. The default
  (`Loose`) silently returns `default` for an unstubbed call, so production code can make extra or wrong
  calls with nothing catching it. When switching a test to Strict causes a failure, add the missing
  `Setup`: that setup documents a contract the test had been ignoring.
- **`Verify` must assert argument values, not just call counts.** `It.IsAny<T>()` in a `Verify` proves the
  method was called, not that it was called correctly — use `It.Is<T>(...)` with the values that matter.
- **Use `SetupSequence` for multi-call sequences**, never a counter variable and a `switch` inside a
  `ReturnsAsync` lambda. The former is explicit; the latter is a hidden state machine.
- **A `[Fact]` that iterates internally must become a `[Theory]`** with `[InlineData]`/`[MemberData]`.
  A loop inside one test entry masks N-1 failing cases behind a single result.
- **No control flow in a test body** — zero `if`, `else`, `switch`, `for`, `foreach`, or `while`. A
  branching test has more than one logical path, so a reader can't tell what it proves and a failing
  branch can mask the others. `foreach` in teardown/`DisposeAsync` is the one exception, because it isn't
  an assertion path.
- **No `ILogger` in tests** and no ad-hoc logging. `ITestOutputHelper` is optional and diagnostic only.

## Test tiers

| Tier | Trait | Project | Requires Azure? | Runs in CI |
|------|-------|---------|-----------------|------------|
| Unit | `Category=Unit` | `Infrastructure.Tests.Unit` | No | Every push/PR |

---

`Infrastructure.Tests.Unit` sets `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>`, so both `dotnet test` (used by CI) and the compiled `.exe` (preferred locally for `-showLiveOutput`) route through the xUnit v3 Microsoft Testing Platform runner.

**The in-process `.exe` runner takes different flags than `dotnet test`.** `--logger console;...`,
`--logger trx`, `--filter-trait`, and `--show-live-output` are *not* recognized by it. Use instead:

| Flag | Effect |
|---|---|
| `-trait "Category=Unit"` | Run only matching trait |
| `-showLiveOutput` | **Critical** — required to see `Console.WriteLine` from fixtures; off by default |
| `-verbose` | Verbose output |
| `-diagnostics` | xUnit-level diagnostics |

## Running Tests Locally

Run from the repo root. No Azure credentials are required: all tests are unit tests.

```powershell
dotnet test --project Infrastructure.Tests.Unit --configuration Debug -- --filter-trait "Category=Unit"

# or the compiled runner, when you want -showLiveOutput:
dotnet build Infrastructure.Tests.Unit --configuration Debug
.\Infrastructure.Tests.Unit\bin\Debug\net10.0\Infrastructure.Tests.Unit.exe -trait "Category=Unit" -showLiveOutput
```

---

## Test coverage

### `HealthChecks/`

One test class per health check. Each check accepts its external dependency via constructor injection, so the class under test is created directly with a test double: an HTTP check gets an `HttpClient` whose handler is `StubHttpMessageHandler.RespondingWith(...)` or `.Throwing(...)`, and every other check a strict Moq mock. No application startup, no Azure, no real services.

| Class | Dependency mocked | What it tests |
|-------|-------------------|---------------|
| `ElasticsearchHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /_cluster/health`: healthy and unhealthy responses |
| `InventoryHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /health` response body == `"Healthy"` |
| `IdentityHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /health` response body == `"Healthy"` |
| `IISHttpsHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | HTTPS reachability of `ServiceEndpointOptions:IisHttps` |
| `KibanaHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /api/status` |
| `ManualsHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /health` response body == `"Healthy"` |
| `MongoDbHealthCheckTests` | `IMongoClient` | `ping` command |
| `PlexHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /identity` |
| `ProductsHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /health` response body == `"Healthy"` |
| `ChurchesHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /health` response body == `"Healthy"` |
| `DirectoryHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /health` response body == `"Healthy"` |
| `CuratorHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /health` response body == `"Healthy"` |
| `LibrarianHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /health` response body == `"Healthy"` |
| `RedisHealthCheckTests` | `IConnectionMultiplexer` | `PING` command |
| `SqlServerHealthCheckTests` | `Func<IDbConnection>` | `SELECT 1` |
| `PostgreSqlHealthCheckTests` | `Func<IDbConnection>` | `SELECT 1`; also asserts both the command and the connection are disposed on the failure paths |
| `YawcamHealthCheckTests` | `Func<TcpClient>` | TCP connect to `YawcamHost:YawcamPort` |
| `WMSvcHealthCheckTests` | `Func<TcpClient>` | TCP connect to `WmsvcHost:WmsvcPort` |
| `HomeAssistantHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /` → HTTP 200 |
| `UptimeKumaHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /` → HTTP 200 |
| `GrafanaHealthCheckTests` | `HttpClient` over `StubHttpMessageHandler` | `GET /api/health` → HTTP 200 |
| `AlloyHealthCheckTests` | `Func<TcpClient>` | TCP connect to `AlloyHost:AlloyPort` |

### `Extensions/`

| Class | What it tests |
|-------|---------------|
| `HttpContextExtensionsTests` | `ListeningAddressUri`, the non-production OIDC redirect base: prefers an `https` listening address over an earlier `http` one, falls back to `http`, trims a trailing slash, and returns `null` when the server lists no address |

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
| `KeepaliveServiceTests` | `KeepaliveService` — no HTTP call when `WEBSITE_HOSTNAME` is unset; self-pings `/ping` when it is set; a failing ping leaves the loop unfaulted. Each timed case waits for `TimerSignalingTimeProvider` to report the delay timer before advancing the clock, and for the stub's first request before stopping, because a .NET 10 `StartAsync` returns before `ExecuteAsync` has run |

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

The analysis needs the repo's SonarCloud token and project, which only maintainers have. C# is analyzed only
through the MSBuild scanner, `dotnet-sonarscanner` (`dotnet tool install --global dotnet-sonarscanner`): the
standalone `sonar-scanner` CLI indexes C# files but publishes no issues, replacing the project's analysis with an
empty one. Run from the repo root, in this order: `begin`, the build, coverage, `end`. The `begin` arguments are
the ones `.github/workflows/main_crgolden-infrastructure.yml` passes, and must stay identical to them. `end` needs
a Java 17 or later runtime in `JAVA_HOME`, process scope only; the `jre` folder of a SonarScanner CLI install is
one. Unit coverage is OpenCover (branch-bearing, via `coverlet.console` pinned in `dotnet-tools.json`). OpenCover
is used rather than Cobertura because it carries branch data, which the quality gate scores against.
Infrastructure is unit-only in CI, so it is the only report produced.

```powershell
$env:SONAR_TOKEN = "<token>"
$env:JAVA_HOME = "<a Java 17+ runtime>"
dotnet tool restore
dotnet-sonarscanner begin /k:"crgolden_Infrastructure" /o:"crgolden" /d:sonar.token="$env:SONAR_TOKEN" `
  /d:sonar.host.url="https://sonarcloud.io" `
  /d:sonar.cs.opencover.reportsPaths="coverage.opencover.xml" `
  /d:sonar.exclusions="**/bin/**,**/obj/**" `
  /d:sonar.coverage.exclusions="**/Program.cs,**/gate.ps1" `
  /d:sonar.qualitygate.wait=true /d:sonar.scanner.skipJreProvisioning=true
dotnet build --no-incremental --configuration Release
dotnet coverlet Infrastructure.Tests.Unit\bin\Release\net10.0 `
  --target "dotnet" `
  --targetargs "test --project Infrastructure.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit" `
  --format opencover --output "coverage.opencover.xml" `
  --skipautoprops --exclude-by-attribute GeneratedCodeAttribute `
  --exclude-by-file "**/obj/**" --exclude-by-file "**/Program.cs" `
  --does-not-return-attribute DoesNotReturnAttribute --include "[Infrastructure]*"

dotnet-sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
```

Required coverage files: `coverage.opencover.xml` (unit, OpenCover).

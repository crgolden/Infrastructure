# Architecture

## Bird's-eye view

One ASP.NET Core app, `Infrastructure`, polls a fixed set of health checks on an interval, keeps the latest snapshot,
pushes each snapshot to signed-in browsers over SignalR, and queues an email message on every status transition. It
sends no email itself: alerts go to an Azure Service Bus queue that a separate worker delivers.

## Codemap

| Part | What it does |
|---|---|
| `Infrastructure/Program.cs` | configuration, every health check registration (inlined, no extension layer), Cookie + OpenID Connect sign-in, SignalR, the Service Bus client, and the production-only telemetry and data protection |
| `Infrastructure/HealthChecks` | one `IHealthCheck` per monitored service; `SiblingAppHealthCheck` and `RelationalHealthCheck` are the shared bases; `HealthCheckNames` and `HealthCheckTags` hold every registration name and tag |
| `Infrastructure/Services/HealthMonitorService.cs` | the `BackgroundService` poll loop: snapshot, SignalR broadcast, transition detection |
| `Infrastructure/Services/AlertService.cs` | queues alert and recovery messages on the Service Bus `email` queue |
| `Infrastructure/Services/KeepaliveService.cs` | self-pings `/ping` every 10 minutes when `WEBSITE_HOSTNAME` is set |
| `Infrastructure/Controllers/StatusController.cs`, `Hubs/HealthHub.cs` | the authorized `/api/status` endpoint and SignalR hub |
| `Infrastructure/Pages` | the dashboard and the sign-out action ([DESIGN.md](DESIGN.md)) |
| `Infrastructure/Models`, `Extensions`, `Telemetry.cs` | options and snapshot types, `GetRequired<T>`, the failure counter |
| `Infrastructure.Tests.Unit` | the unit tier ([TESTING.md](TESTING.md)) |
| `AGENTS.md` | the invariants and the steps for adding a monitored service, written for coding agents |
| `.github/workflows/main_crgolden-infrastructure.yml` | build, inspect, test, analyze and deploy |
| `NuGet.Config`, `packages.lock.json` files, `dotnet-tools.json`, `global.json` | package sources (the `Shared` package from GitHub Packages), locked restore, pinned tools, the SDK |
| `gate.ps1` | the maintainers' local gate (README.md, "Gate") |

## Invariants

- **Every check takes its dependency by constructor**: a typed `HttpClient`, a `Func<TcpClient>`, an
  `IConnectionMultiplexer`, an `IMongoClient`, or a keyed `Func<IDbConnection>`. That is what lets every check be
  unit-tested with no running service.
- **Both relational connection factories are keyed** (`"SqlServer"`, `"PostgreSql"`), and none is unkeyed, so a check
  that loses its key fails at construction instead of silently probing the other database.
- **A web-app check is healthy only when `/health` answers exactly `Healthy`.**
- **Only transitions alert**: Healthy or Unknown to Unhealthy sends an alert, Unhealthy to Healthy a recovery, Degraded
  nothing.
- **The relational checks retry once** (250 ms) before reporting Unhealthy, and validate the server's TLS certificate,
  so they fail for the same reasons a real client fails.
- **`/health` reports this app only** (`Predicate = _ => false`): a monitored service being down is data, not a fault
  of this app, and the health-check framework's own error logging is turned down for the same reason.
- **Configuration is read the same way in every environment**, through `GetRequired<T>`; a `null` in
  `appsettings.json` marks a value that must be supplied.

## Boundaries

- The dashboard, `/api/status` and the hub require sign-in; `/health` and `/ping` are anonymous so they can be
  monitored. The app holds no tokens for downstream calls.
- Outbound: every monitored service, and the Service Bus `email` queue. Each monitored host must accept connections
  from wherever the app runs.

## Cross-cutting concerns

- **The poll loop and the keepalive suppress their own telemetry**, so probing every service each cycle emits no
  spans; transitions surface through alerts and SignalR instead. Telemetry is production-only: OpenTelemetry over OTLP
  with one counter, `infrastructure.health_monitor.failures`, and Serilog to an Elasticsearch data stream.
- **Warnings are errors**, and CI also fails on any `jb inspectcode` result and any open SonarCloud issue.
- **Publishing needs `-r win-x86`**: the hosting tier is 32-bit only.

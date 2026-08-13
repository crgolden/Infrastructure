# AGENTS.md

Agent-facing guide for this repo. Self-contained by design: nothing here links to or depends on a file
outside this repository root. Human-facing setup, the monitored-service catalog, and the full
configuration-key reference are in [README.md](README.md); test layout, coverage, and local SonarCloud
analysis are in [TESTING.md](TESTING.md).

## What this app is

An ASP.NET Core 10 service-health dashboard. `Services/HealthMonitorService` is a `BackgroundService` that
polls every registered `IHealthCheck` on an interval, stores the latest `HealthSnapshot`, broadcasts it over
SignalR (`Hubs/HealthHub` at `/hubs/health`, client method `ReceiveSnapshot`), and hands status transitions
to `Services/AlertService`. Alerts are *enqueued* to a Service Bus queue — a separate worker application
performs the actual email delivery. This repo never sends mail itself.

## Invariants

- **Every `IHealthCheck` takes its dependency by constructor injection** — never a service locator, never a
  `new` inside `CheckHealthAsync`. A typed `HttpClient` (`AddHttpClient<TCheck>`) for HTTP checks,
  `Func<TcpClient>` for TCP checks, `IConnectionMultiplexer` for Redis, `IMongoClient` for MongoDB,
  `Func<IDbConnection>` for the relational checks. This is the whole reason the suite can use
  `MockBehavior.Strict` with no integration tests.
- **`PostgreSqlHealthCheck` resolves a *keyed* factory** — `[FromKeyedServices("PostgreSql")]
  Func<IDbConnection>`, registered via `AddKeyedTransient`. `SqlServerHealthCheck` takes the unkeyed
  default. Two relational checks share one delegate type, so dropping the key silently points PostgreSQL at
  SQL Server.
- **All registration is inlined in `Program.cs`.** There is deliberately no `ServiceCollectionExtensions`
  layer in this repo. Add new checks in place rather than introducing one.
- **Sibling-app checks extend `SiblingAppHealthCheck`.** The base resolves the target's base URL from a
  configuration key and reports healthy only when the response body equals `Healthy` exactly — not a 200
  status, not a JSON payload. A sibling that changes its `/health` response shape breaks this check, and
  the failure looks like an outage rather than a contract change.
- **Configuration is read identically in every environment**, through
  `Extensions/ConfigurationExtensions.GetRequired<T>()`. `DefaultAzureCredential` is constructed only on
  the production path; non-production reads the same keys from User Secrets or environment variables. There
  is no `IsDevelopment()` branch in the config-reading code and none should be added — a `null` in
  `appsettings.json` is the signal that a value must be supplied externally.
- **Both background services suppress their own telemetry** with `SuppressInstrumentationScope.Begin()` —
  `HealthMonitorService` around the per-poll probes (otherwise every outbound HTTP/SQL call to every
  monitored service emits a span each cycle), and `KeepaliveService` around its self-ping. Removing either
  floods the trace backend with noise that carries no signal; transitions are surfaced through
  `AlertService` and SignalR instead.
- **Only transitions alert, never steady state.** `Unknown`/`Healthy` → `Unhealthy` sends an alert;
  `Unhealthy` → `Healthy` sends a recovery. `Degraded` sends nothing. Alerting on current status rather
  than on the edge would mail on every poll for the duration of an outage.
- **`PostgreSqlHealthCheck` and `SqlServerHealthCheck` retry once (250ms delay) before reporting
  `Unhealthy`.** Diagnosed 2026-08-07: the self-hosted PostgreSQL server and its firewall rule were both
  confirmed healthy and current, but the server's own log showed zero trace of the failing connection
  attempts on the days they occurred — the TCP connect (governed by the 30s `Timeout` in
  `NpgsqlConnectionStringBuilder`/`SqlConnectionStringBuilder`) was being dropped in transit between
  Azure and the host, not rejected by the database. A single-attempt check turns a rare, self-clearing
  network blip into a full alert cycle; the retry absorbs that without masking a real outage, since a
  sustained problem still fails both attempts.

- **A monitored service being down is data, not an Infrastructure fault.** Two consequences, both load-
  bearing. First, `/health` maps the health-check registry with `Predicate = _ => false`, so it reports
  only whether *this app* is running — never the aggregate of the fleet it watches. Registering the
  monitored services in the framework registry is a storage decision; it does not make them dependencies
  of this app. Second, `Microsoft.Extensions.Diagnostics.HealthChecks` is overridden to `Fatal` in
  `appsettings.json`, because `DefaultHealthCheckService` logs `HealthCheckEnd` at **Error** for every
  check that returns `Unhealthy` (and `Warning` for `Degraded`) — one entry per affected service per
  poll, for as long as that service stays down. `Override` is used rather than a lower `MinimumLevel`
  because Serilog's minimum level can only suppress events *below* a level, never downgrade an `Error`;
  and `Override` correctly outranks whatever `MinimumLevel:Default` the deployment environment sets.
  Every message from that
  category describes a monitored service, never this app: every one of the 22 registered checks swallows
  its own exceptions — 14 in the check itself, the 8 sibling checks through `SiblingAppHealthCheck`'s
  catch-all — so the framework's `HealthCheckError` path is unreachable, and a fault in
  the poll loop itself surfaces through `HealthMonitorService`'s own `ILogger` instead. Restoring either
  behaviour re-creates the incident below.
- **The reason both of the above exist.** Curator was taken down deliberately for E2E work on 2026-08-12.
  Infrastructure emailed the alert and the recovery exactly as designed — and then, for the 13½ hours in
  between, emitted an Error-level entry on every poll, which held the Elasticsearch Error/Fatal
  log-volume alert in Grafana firing for the whole window. A planned single-service outage therefore read
  as a sustained failure *of the monitoring app*, which is the one thing this app must never
  misreport. None of those entries carried signal the dashboard, the SignalR feed, and the transition
  emails don't already carry.

## Adding a monitored service

1. Add the check under `HealthChecks/`. Extend `SiblingAppHealthCheck` if the target is a sibling app with
   a `/health` endpoint; otherwise implement `IHealthCheck` and take its client by constructor injection.
2. Register it in `Program.cs` beside the others, with its typed `HttpClient` or factory if it needs one.
3. Add the configuration key(s) to `appsettings.json` with a `null` value.
4. Document the key in README.md's configuration tables and the service in its monitored-services table.
5. Add unit tests covering healthy, unhealthy, and throwing-dependency paths — see TESTING.md.

## Gotchas

- **`dotnet publish` requires `-r win-x86`.** The hosting tier is 32-bit only; an x64 publish deploys
  successfully and then fails at startup, which reads as a deployment problem rather than an architecture
  mismatch.
- **The dashboard, `/api/status`, and the SignalR hub are all `[Authorize]`** behind Cookie +
  OpenIdConnect — this app has no BFF and holds no tokens for downstream calls. `/health` and `/ping` are
  anonymous on purpose: a monitoring endpoint that requires a login cannot be monitored.
- **`/api/status` returns 503 until the first poll completes.** That is correct behaviour and is asserted
  by `StatusControllerTests`. Don't paper over it by seeding an empty snapshot.
- **`KeepaliveService` is inert unless `WEBSITE_HOSTNAME` is set**, so it does nothing locally. Its absence
  in development is not a bug.
- **Trace filtering excludes `/health` and `/ping`** via `AspNetCoreTraceInstrumentationOptions.Filter`.
  Adding a new anonymous liveness route means adding it to that filter too, or every probe from every
  external monitor becomes a span.

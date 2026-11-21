namespace Infrastructure.Services;

using Infrastructure.Hubs;
using Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry;

public sealed class HealthMonitorService : BackgroundService, IHealthMonitorService
{
    internal const string SnapshotClientMethod = "ReceiveSnapshot";

    private readonly Dictionary<string, AlertTracking> _alertTracking = [];
    private readonly Lock _lock = new();
    private readonly HealthCheckService _healthCheckService;
    private readonly IHubContext<HealthHub> _hubContext;
    private readonly IAlertService _alertService;
    private readonly Telemetry _telemetry;
    private readonly int _intervalSeconds;
    private readonly int _alertAfterConsecutiveFailures;

    private HealthSnapshot? _lastSnapshot;

    public HealthMonitorService(
        HealthCheckService healthCheckService,
        IHubContext<HealthHub> hubContext,
        IAlertService alertService,
        IOptions<MonitoringOptions> options,
        Telemetry telemetry)
    {
        _healthCheckService = healthCheckService;
        _hubContext = hubContext;
        _alertService = alertService;
        _telemetry = telemetry;
        if (!options.Value.IntervalSeconds.HasValue)
        {
            throw new InvalidOperationException($"Invalid '{nameof(MonitoringOptions.IntervalSeconds)}'.");
        }

        _intervalSeconds = options.Value.IntervalSeconds.Value;
        if (options.Value.AlertAfterConsecutiveFailures is not > 0)
        {
            throw new InvalidOperationException($"Invalid '{nameof(MonitoringOptions.AlertAfterConsecutiveFailures)}'.");
        }

        _alertAfterConsecutiveFailures = options.Value.AlertAfterConsecutiveFailures.Value;
    }

    public HealthSnapshot? LastSnapshot
    {
        get
        {
            lock (_lock)
            {
                return _lastSnapshot;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }

    private static ServiceStatus MapStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => ServiceStatus.Healthy,
        HealthStatus.Degraded => ServiceStatus.Degraded,
        HealthStatus.Unhealthy => ServiceStatus.Unhealthy,
        _ => ServiceStatus.Unknown,
    };

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        HealthReport report;
        try
        {
            using (SuppressInstrumentationScope.Begin())
            {
                report = await _healthCheckService.CheckHealthAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.HealthMonitorFailed(Telemetry.Metrics.PollStage, ex);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var results = report.Entries.Select(entry => new ServiceHealthResult(
            Name: entry.Key,
            Status: MapStatus(entry.Value.Status),
            Description: entry.Value.Description,
            CheckedAt: now)).ToList();

        var snapshot = new HealthSnapshot(now, results);

        lock (_lock)
        {
            _lastSnapshot = snapshot;
        }

        try
        {
            await _hubContext.Clients.All.SendAsync(SnapshotClientMethod, snapshot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.HealthMonitorFailed(Telemetry.Metrics.SnapshotPushStage, ex);
        }

        foreach (var result in results)
        {
            _alertTracking.TryGetValue(result.Name, out var previous);
            var decision = AlertTransitions.Decide(previous, result.Status, _alertAfterConsecutiveFailures);

            try
            {
                switch (decision.Action)
                {
                    case AlertAction.SendAlert:
                        await _alertService.SendAlertAsync(result, cancellationToken);
                        break;
                    case AlertAction.SendRecovery:
                        await _alertService.SendRecoveryAsync(result, cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _telemetry.HealthMonitorFailed(Telemetry.Metrics.AlertSendStage, ex);
            }

            _alertTracking[result.Name] = decision.Tracking;
        }
    }
}

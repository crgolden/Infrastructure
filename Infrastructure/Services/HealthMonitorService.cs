namespace Infrastructure.Services;

using Infrastructure.Hubs;
using Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

public sealed class HealthMonitorService(
    HealthCheckService healthCheckService,
    IHubContext<HealthHub> hubContext,
    IAlertService alertService,
    IOptions<MonitoringOptions> options,
    ILogger<HealthMonitorService> logger) : BackgroundService, IHealthMonitorService
{
    private HealthSnapshot? _lastSnapshot;
    private readonly Dictionary<string, ServiceStatus> _previousStatuses = [];
    private readonly Lock _lock = new();

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
        logger.LogInformation("Health monitor started. Polling every {Seconds}s.", options.Value.IntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(options.Value.IntervalSeconds), stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);
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

        await hubContext.Clients.All.SendAsync("ReceiveSnapshot", snapshot, cancellationToken);

        foreach (var result in results)
        {
            _previousStatuses.TryGetValue(result.Name, out var previous);
            var current = result.Status;

            if (previous == ServiceStatus.Healthy && current == ServiceStatus.Unhealthy)
            {
                logger.LogWarning("Service {Name} transitioned to Unhealthy: {Description}", result.Name, result.Description);
                await alertService.SendAlertAsync(result, cancellationToken);
            }
            else if (previous == ServiceStatus.Unhealthy && current == ServiceStatus.Healthy)
            {
                logger.LogInformation("Service {Name} recovered to Healthy.", result.Name);
                await alertService.SendRecoveryAsync(result, cancellationToken);
            }

            _previousStatuses[result.Name] = current;
        }
    }

    private static ServiceStatus MapStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => ServiceStatus.Healthy,
        HealthStatus.Degraded => ServiceStatus.Degraded,
        HealthStatus.Unhealthy => ServiceStatus.Unhealthy,
        _ => ServiceStatus.Unknown,
    };
}

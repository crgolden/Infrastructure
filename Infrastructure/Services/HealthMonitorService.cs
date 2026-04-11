namespace Infrastructure.Services;

using Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

public sealed partial class HealthMonitorService : BackgroundService, IHealthMonitorService
{
    private readonly Dictionary<string, ServiceStatus> _previousStatuses = [];
    private readonly Lock _lock = new();
    private readonly HealthCheckService _healthCheckService;
    private readonly IHubContext<HealthHub> _hubContext;
    private readonly IAlertService _alertService;
    private readonly int _intervalSeconds;
    private readonly ILogger<HealthMonitorService> _logger;

    private HealthSnapshot? _lastSnapshot;

    public HealthMonitorService(
        HealthCheckService healthCheckService,
        IHubContext<HealthHub> hubContext,
        IAlertService alertService,
        IOptions<MonitoringOptions> options,
        ILogger<HealthMonitorService> logger)
    {
        _healthCheckService = healthCheckService;
        _hubContext = hubContext;
        _alertService = alertService;
        if (!options.Value.IntervalSeconds.HasValue)
        {
            throw new InvalidOperationException($"Invalid '{nameof(MonitoringOptions.IntervalSeconds)}'.");
        }

        _intervalSeconds = options.Value.IntervalSeconds.Value;
        _logger = logger;
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
        LogMonitorStarted(_logger, _intervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Health monitor started. Polling every {Seconds}s.")]
    private static partial void LogMonitorStarted(ILogger logger, int seconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service {Name} recovered to Healthy.")]
    private static partial void LogServiceRecovered(ILogger logger, string name);

    private static ServiceStatus MapStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => ServiceStatus.Healthy,
        HealthStatus.Degraded => ServiceStatus.Degraded,
        HealthStatus.Unhealthy => ServiceStatus.Unhealthy,
        _ => ServiceStatus.Unknown,
    };

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
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

        await _hubContext.Clients.All.SendAsync("ReceiveSnapshot", snapshot, cancellationToken);

        foreach (var result in results)
        {
            _previousStatuses.TryGetValue(result.Name, out var previous);
            var current = result.Status;

            switch (previous)
            {
                case ServiceStatus.Healthy when current is ServiceStatus.Unhealthy:
                    _logger.LogWarning("Service {Name} transitioned to Unhealthy: {Description}", result.Name, result.Description);
                    await _alertService.SendAlertAsync(result, cancellationToken);
                    break;
                case ServiceStatus.Unhealthy when current is ServiceStatus.Healthy:
                    LogServiceRecovered(_logger, result.Name);
                    await _alertService.SendRecoveryAsync(result, cancellationToken);
                    break;
            }

            _previousStatuses[result.Name] = current;
        }
    }
}

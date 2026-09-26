namespace Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Infrastructure.Models;
using Microsoft.Extensions.Options;

public sealed class Telemetry
{
    private readonly Counter<long> _healthMonitorFailureCounter;

    public Telemetry(IMeterFactory meterFactory, IOptions<TelemetryOptions> telemetryOptions)
    {
        var meter = meterFactory.Create(Metrics.MeterName, typeof(Telemetry).Assembly.GetName().Version?.ToString());
        _healthMonitorFailureCounter = meter.CreateCounter<long>(
            Metrics.HealthMonitorFailureCounterName,
            description: telemetryOptions.Value.HealthMonitorFailureDescription);
    }

    public void HealthMonitorFailed(string stage, Exception exception) =>
        _healthMonitorFailureCounter.Add(
            1,
            new TagList
            {
                { Metrics.StageTagName, stage },
                { Metrics.ExceptionTypeTagName, exception.GetType().FullName },
            });

    internal static class Metrics
    {
        public const string MeterName = nameof(Infrastructure);

        public const string HealthMonitorFailureCounterName = "infrastructure.health_monitor.failures";

        public const string StageTagName = "stage";

        public const string ExceptionTypeTagName = "exception.type";

        public const string PollStage = "poll";

        public const string SnapshotPushStage = "snapshot-push";

        public const string AlertSendStage = "alert-send";
    }
}

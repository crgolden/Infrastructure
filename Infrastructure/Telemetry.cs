namespace Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;

internal static class Telemetry
{
    internal static class Metrics
    {
        public const string MeterName = nameof(Infrastructure);

        public const string HealthMonitorFailureCounterName = "infrastructure.health_monitor.failures";

        public const string StageTagName = "stage";

        public const string ExceptionTypeTagName = "exception.type";

        public const string PollStage = "poll";

        public const string SnapshotPushStage = "snapshot-push";

        public const string AlertSendStage = "alert-send";

        private static readonly Meter Meter = new(MeterName, "1.0.0");

        private static readonly Counter<long> HealthMonitorFailureCounter =
            Meter.CreateCounter<long>(
                HealthMonitorFailureCounterName,
                description: "Exceptions caught and handled inside a health-monitor poll, split by which stage of the poll threw.");

        public static void HealthMonitorFailed(string stage, Exception exception) =>
            HealthMonitorFailureCounter.Add(
                1,
                new TagList
                {
                    { StageTagName, stage },
                    { ExceptionTypeTagName, exception.GetType().FullName },
                });
    }
}

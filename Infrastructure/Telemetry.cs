namespace Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public static class Telemetry
{
    public static readonly ActivitySource ActivitySource = new(nameof(Infrastructure), "1.0.0");

    private static readonly Meter Meter = new(nameof(Infrastructure), "1.0.0");

    public static class Metrics
    {
        public static readonly Counter<int> StatusTransitions =
            Meter.CreateCounter<int>(
                "infrastructure.health.status_transition",
                description: "Number of health status transitions per service.");
    }
}

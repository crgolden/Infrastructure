namespace Infrastructure.Models;

public sealed class MonitoringOptions
{
    public int? IntervalSeconds { get; set; }

    public int? AlertAfterConsecutiveFailures { get; set; }
}

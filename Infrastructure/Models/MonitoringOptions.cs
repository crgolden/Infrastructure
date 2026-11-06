namespace Infrastructure.Models;

public sealed class MonitoringOptions
{
    public int? IntervalSeconds { get; set; }

    public int? UnhealthyPollsBeforeAlert { get; set; }
}

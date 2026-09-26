namespace Infrastructure.Tests.Unit;

using Infrastructure.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class TelemetryTests : IDisposable
{
    private readonly TelemetryHarness _harness = new();

    [Fact]
    public void HealthMonitorFailureCounter_CarriesTheConfiguredDescription()
    {
        // Arrange
        Dictionary<string, string?> expected = new(StringComparer.Ordinal)
        {
            [Telemetry.Metrics.HealthMonitorFailureCounterName] = _harness.Descriptions.HealthMonitorFailureDescription,
        };

        // Act
        var actual = _harness.PublishedDescriptions();

        // Assert
        Assert.Equal(expected, actual);
    }

    public void Dispose() => _harness.Dispose();
}

namespace Infrastructure.Models;

public sealed record HealthSnapshot(
    DateTimeOffset Timestamp,
    IReadOnlyList<ServiceHealthResult> Results);

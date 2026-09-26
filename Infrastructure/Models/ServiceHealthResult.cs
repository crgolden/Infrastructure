namespace Infrastructure.Models;

public sealed record ServiceHealthResult(
    string Name,
    ServiceStatus Status,
    string? Description,
    DateTimeOffset CheckedAt);

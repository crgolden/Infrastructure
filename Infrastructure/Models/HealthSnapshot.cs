namespace Infrastructure.Models;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record HealthSnapshot(
    DateTimeOffset Timestamp,
    IReadOnlyList<ServiceHealthResult> Results);

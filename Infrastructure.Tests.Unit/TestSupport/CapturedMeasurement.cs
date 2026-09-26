namespace Infrastructure.Tests.Unit.TestSupport;

internal sealed record CapturedMeasurement(long Value, IReadOnlyDictionary<string, string?> Tags);

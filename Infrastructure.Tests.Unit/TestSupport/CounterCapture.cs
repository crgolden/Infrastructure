namespace Infrastructure.Tests.Unit.TestSupport;

using System.Diagnostics.Metrics;

internal sealed class CounterCapture : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<CapturedMeasurement> _measurements = [];
    private readonly Lock _lock = new();
    private readonly TaskCompletionSource _firstMeasurement =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CounterCapture(string meterName, string instrumentName)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal)
                    && string.Equals(instrument.Name, instrumentName, StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.Start();
    }

    public IReadOnlyList<CapturedMeasurement> Measurements
    {
        get
        {
            lock (_lock)
            {
                return [.. _measurements];
            }
        }
    }

    public Task FirstMeasurement => _firstMeasurement.Task;

    public void Dispose() => _listener.Dispose();

    private void OnMeasurement(
        Instrument instrument,
        long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        var tagsByName = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            tagsByName[tag.Key] = tag.Value?.ToString();
        }

        var captured = new CapturedMeasurement(measurement, tagsByName);
        lock (_lock)
        {
            _measurements.Add(captured);
        }

        _firstMeasurement.TrySetResult();
    }
}

internal sealed record CapturedMeasurement(long Value, IReadOnlyDictionary<string, string?> Tags);

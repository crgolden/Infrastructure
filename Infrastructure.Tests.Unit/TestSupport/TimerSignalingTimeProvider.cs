namespace Infrastructure.Tests.Unit.TestSupport;

using Microsoft.Extensions.Time.Testing;

internal sealed class TimerSignalingTimeProvider : FakeTimeProvider
{
    private readonly TaskCompletionSource _firstTimerCreated =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task FirstTimerCreated => _firstTimerCreated.Task;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        _firstTimerCreated.TrySetResult();
        return timer;
    }
}

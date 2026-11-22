namespace Infrastructure.Services;

using Infrastructure.Models;

internal enum AlertAction
{
    None,
    SendAlert,
    SendRecovery,
}

internal readonly record struct AlertTracking(int ConsecutiveUnhealthy, bool Alerted);

internal readonly record struct AlertDecision(AlertTracking Tracking, AlertAction Action);

internal static class AlertTransitions
{
    public static AlertDecision Decide(
        AlertTracking previous,
        ServiceStatus current,
        int alertAfterConsecutiveFailures,
        bool inMaintenance)
    {
        if (current is ServiceStatus.Unhealthy)
        {
            var count = previous.ConsecutiveUnhealthy + 1;
            var crossed = !inMaintenance && !previous.Alerted && count >= alertAfterConsecutiveFailures;
            return new AlertDecision(
                new AlertTracking(count, previous.Alerted || crossed),
                crossed ? AlertAction.SendAlert : AlertAction.None);
        }

        if (current is ServiceStatus.Healthy)
        {
            return new AlertDecision(
                new AlertTracking(0, false),
                previous.Alerted ? AlertAction.SendRecovery : AlertAction.None);
        }

        return new AlertDecision(previous with { ConsecutiveUnhealthy = 0 }, AlertAction.None);
    }
}

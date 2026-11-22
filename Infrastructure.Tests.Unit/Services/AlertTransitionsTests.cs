namespace Infrastructure.Tests.Unit.Services;

using Infrastructure.Models;
using Infrastructure.Services;

[Trait("Category", "Unit")]
public sealed class AlertTransitionsTests
{
    [Fact]
    public void Decide_UnhealthyBelowTheThreshold_SendsNothing()
    {
        // Arrange
        var threshold = Random.Shared.Next(3, 10);
        var failuresSoFar = Random.Shared.Next(0, threshold - 1);
        var previous = new AlertTracking(failuresSoFar, Alerted: false);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Unhealthy, threshold, inMaintenance: false);

        // Assert
        Assert.Equal(new AlertDecision(new AlertTracking(failuresSoFar + 1, Alerted: false), AlertAction.None), decision);
    }

    [Fact]
    public void Decide_UnhealthyReachingTheThreshold_SendsOneAlert()
    {
        // Arrange
        var threshold = Random.Shared.Next(2, 10);
        var previous = new AlertTracking(threshold - 1, Alerted: false);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Unhealthy, threshold, inMaintenance: false);

        // Assert
        Assert.Equal(new AlertDecision(new AlertTracking(threshold, Alerted: true), AlertAction.SendAlert), decision);
    }

    [Fact]
    public void Decide_UnhealthyAfterTheAlert_SendsNothingMore()
    {
        // Arrange
        var threshold = Random.Shared.Next(2, 10);
        var previous = new AlertTracking(threshold, Alerted: true);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Unhealthy, threshold, inMaintenance: false);

        // Assert
        Assert.Equal(new AlertDecision(new AlertTracking(threshold + 1, Alerted: true), AlertAction.None), decision);
    }

    [Fact]
    public void Decide_HealthyAfterAnAlert_SendsOneRecovery()
    {
        // Arrange
        var threshold = Random.Shared.Next(2, 10);
        var previous = new AlertTracking(threshold, Alerted: true);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Healthy, threshold, inMaintenance: false);

        // Assert
        Assert.Equal(new AlertDecision(new AlertTracking(0, Alerted: false), AlertAction.SendRecovery), decision);
    }

    [Fact]
    public void Decide_HealthyAfterABlipThatNeverAlerted_SendsNoRecovery()
    {
        // Arrange
        var threshold = Random.Shared.Next(3, 10);
        var previous = new AlertTracking(threshold - 1, Alerted: false);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Healthy, threshold, inMaintenance: false);

        // Assert
        Assert.Equal(new AlertDecision(new AlertTracking(0, Alerted: false), AlertAction.None), decision);
    }

    [Fact]
    public void Decide_DegradedResetsTheCountAndKeepsAnEarlierAlert()
    {
        // Arrange
        var threshold = Random.Shared.Next(2, 10);
        var previous = new AlertTracking(threshold, Alerted: true);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Degraded, threshold, inMaintenance: false);

        // Assert
        Assert.Equal(new AlertDecision(new AlertTracking(0, Alerted: true), AlertAction.None), decision);
    }

    [Fact]
    public void Decide_FirstPollUnhealthyAtTheMinimumThreshold_AlertsAtOnce()
    {
        // Arrange
        var previous = default(AlertTracking);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Unhealthy, alertAfterConsecutiveFailures: 1, inMaintenance: false);

        // Assert
        Assert.Equal(AlertAction.SendAlert, decision.Action);
    }

    [Fact]
    public void Decide_UnhealthyReachingTheThresholdInAMaintenanceWindow_SendsNothingAndStaysUnalerted()
    {
        // Arrange
        var threshold = Random.Shared.Next(2, 10);
        var previous = new AlertTracking(threshold - 1, Alerted: false);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Unhealthy, threshold, inMaintenance: true);

        // Assert
        Assert.Equal(new AlertDecision(new AlertTracking(threshold, Alerted: false), AlertAction.None), decision);
    }

    [Fact]
    public void Decide_StillUnhealthyOnTheFirstPollAfterAMaintenanceWindow_SendsOneAlert()
    {
        // Arrange
        var threshold = Random.Shared.Next(2, 10);
        var previous = new AlertTracking(threshold, Alerted: false);

        // Act
        var decision = AlertTransitions.Decide(previous, ServiceStatus.Unhealthy, threshold, inMaintenance: false);

        // Assert
        Assert.Equal(new AlertDecision(new AlertTracking(threshold + 1, Alerted: true), AlertAction.SendAlert), decision);
    }
}

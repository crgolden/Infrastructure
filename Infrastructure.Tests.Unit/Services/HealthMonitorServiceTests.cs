namespace Infrastructure.Tests.Unit.Services;

using Infrastructure;
using Infrastructure.Hubs;
using Infrastructure.Models;
using Infrastructure.Services;
using Infrastructure.Tests.Unit.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;

[Trait("Category", "Unit")]
public sealed class HealthMonitorServiceTests : IDisposable
{
    private static readonly string MonitoredServiceName = Generated.NewMonitoredServiceName();

    private readonly TelemetryHarness _harness = new();

    [Fact]
    public async Task ExecuteAsync_StoresSnapshotAfterFirstPoll()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        var snapshotPushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => snapshotPushed.TrySetResult())
            .Returns(Task.CompletedTask);

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await snapshotPushed.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert
        Assert.NotNull(svc.LastSnapshot);
    }

    [Fact]
    public async Task ExecuteAsync_SendsAlertWhenServiceIsUnhealthyOnFirstPoll()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var alertSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Callback(() => alertSent.TrySetResult())
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await alertSent.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        alertService.Verify(
            a => a.SendAlertAsync(
                It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Unhealthy),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SendsAlertWhenServiceTransitionsToUnhealthy()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService
            .SetupSequence(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var alertSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Callback(() => alertSent.TrySetResult())
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await alertSent.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        alertService.Verify(
            a => a.SendAlertAsync(
                It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Unhealthy),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SendsRecoveryWhenServiceReturnsToHealthy()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService
            .SetupSequence(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var recoverySent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        alertService.Setup(a => a.SendRecoveryAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Callback(() => recoverySent.TrySetResult())
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await recoverySent.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        alertService.Verify(
            a => a.SendRecoveryAsync(
                It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Healthy),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_PushesSnapshotToHub()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        var snapshotPushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => snapshotPushed.TrySetResult())
            .Returns(Task.CompletedTask);

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await snapshotPushed.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        clientProxy.Verify(
            c => c.SendCoreAsync(
                HealthMonitorService.SnapshotClientMethod,
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WhenIntervalSecondsIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);

        // Act
        var exception = Record.Exception(() => new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            Options.Create(new MonitoringOptions { IntervalSeconds = null }),
            _harness.Telemetry));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task ExecuteAsync_WithDegradedService_MapsToDegradedStatus()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Degraded));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        var snapshotPushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => snapshotPushed.TrySetResult())
            .Returns(Task.CompletedTask);

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await snapshotPushed.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert
        Assert.NotNull(svc.LastSnapshot);
        Assert.Contains(svc.LastSnapshot.Results, r => r.Status == ServiceStatus.Degraded);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesPollingWhenCheckHealthAsyncThrows()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService
            .SetupSequence(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(Generated.NewFailureMessage()))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        var snapshotPushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => snapshotPushed.TrySetResult())
            .Returns(Task.CompletedTask);

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await snapshotPushed.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert
        Assert.NotNull(svc.LastSnapshot);
        clientProxy.Verify(
            c => c.SendCoreAsync(
                HealthMonitorService.SnapshotClientMethod, It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesPollingWhenSignalRThrows()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var alertSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Callback(() => alertSent.TrySetResult())
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await alertSent.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        alertService.Verify(
            a => a.SendAlertAsync(
                It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Unhealthy),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesPollingWhenAlertServiceThrows()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService
            .SetupSequence(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var recoverySent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));
        alertService.Setup(a => a.SendRecoveryAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Callback(() => recoverySent.TrySetResult())
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await recoverySent.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        alertService.Verify(
            a => a.SendRecoveryAsync(
                It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Healthy),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCheckHealthAsyncThrows_CountsAPollStageFailure()
    {
        // Arrange
        using var capture = new CounterCapture(
            _harness.MeterFactory,
            Telemetry.Metrics.HealthMonitorFailureCounterName);

        var pollFailureMessage = $"poll-failure-{Guid.NewGuid()}";
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService
            .SetupSequence(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(pollFailureMessage))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            _harness.Telemetry);

        // Act
        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await capture.FirstMeasurement.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert
        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal(Telemetry.Metrics.PollStage, measurement.Tags[Telemetry.Metrics.StageTagName]);
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            measurement.Tags[Telemetry.Metrics.ExceptionTypeTagName]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnhealthyForFewerPollsThanTheThreshold_SendsNoAlertAndNoRecovery()
    {
        // Arrange
        var unhealthyPollsBeforeAlert = Generated.NewConsecutiveFailureCount();
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UnhealthyThenHealthy(unhealthyPollsBeforeAlert - 1));
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        var healthyPollSeen = PushedSnapshots(clientProxy, unhealthyPollsBeforeAlert + 1);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        var svc = new HealthMonitorService(
            healthCheckService.Object,
            HubContextFor(clientProxy).Object,
            alertService.Object,
            GetImmediateOptions(unhealthyPollsBeforeAlert),
            _harness.Telemetry);
        using var cts = new CancellationTokenSource();

        // Act
        _ = svc.StartAsync(cts.Token);
        await healthyPollSeen.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert
        alertService.Verify(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()), Times.Never);
        alertService.Verify(a => a.SendRecoveryAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnhealthyForTheThresholdAndBeyond_SendsOneAlert()
    {
        // Arrange
        var unhealthyPollsBeforeAlert = Generated.NewConsecutiveFailureCount();
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Unhealthy));
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        var pollsPastTheThreshold = PushedSnapshots(clientProxy, unhealthyPollsBeforeAlert + 2);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var svc = new HealthMonitorService(
            healthCheckService.Object,
            HubContextFor(clientProxy).Object,
            alertService.Object,
            GetImmediateOptions(unhealthyPollsBeforeAlert),
            _harness.Telemetry);
        using var cts = new CancellationTokenSource();

        // Act
        _ = svc.StartAsync(cts.Token);
        await pollsPastTheThreshold.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert
        alertService.Verify(
            a => a.SendAlertAsync(It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Unhealthy), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHealthyAfterAnAlert_SendsARecovery()
    {
        // Arrange
        var unhealthyPollsBeforeAlert = Generated.NewConsecutiveFailureCount();
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UnhealthyThenHealthy(unhealthyPollsBeforeAlert));
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var recoverySent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        alertService.Setup(a => a.SendRecoveryAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Callback(() => recoverySent.TrySetResult())
            .Returns(Task.CompletedTask);
        var svc = new HealthMonitorService(
            healthCheckService.Object,
            HubContextFor(clientProxy).Object,
            alertService.Object,
            GetImmediateOptions(unhealthyPollsBeforeAlert),
            _harness.Telemetry);
        using var cts = new CancellationTokenSource();

        // Act
        _ = svc.StartAsync(cts.Token);
        await recoverySent.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert
        alertService.Verify(
            a => a.SendAlertAsync(It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Unhealthy), It.IsAny<CancellationToken>()),
            Times.Once);
        alertService.Verify(
            a => a.SendRecoveryAsync(It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Healthy), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WhenUnhealthyPollsBeforeAlertIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        var options = Options.Create(new MonitoringOptions { IntervalSeconds = 1, UnhealthyPollsBeforeAlert = null });

        // Act
        var exception = Record.Exception(() => new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            options,
            _harness.Telemetry));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    public void Dispose() => _harness.Dispose();

    private static IOptions<MonitoringOptions> GetDefaultOptions() =>
        Options.Create(new MonitoringOptions { IntervalSeconds = 1, UnhealthyPollsBeforeAlert = 1 });

    private static IOptions<MonitoringOptions> GetImmediateOptions(int unhealthyPollsBeforeAlert) =>
        Options.Create(new MonitoringOptions { IntervalSeconds = 0, UnhealthyPollsBeforeAlert = unhealthyPollsBeforeAlert });

    private static Func<HealthReport> UnhealthyThenHealthy(int unhealthyPolls)
    {
        var remaining = unhealthyPolls;
        return () => BuildReport(Interlocked.Decrement(ref remaining) >= 0 ? HealthStatus.Unhealthy : HealthStatus.Healthy);
    }

    private static Task PushedSnapshots(Mock<IClientProxy> clientProxy, int snapshots)
    {
        var pushed = 0;
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref pushed) >= snapshots)
                {
                    reached.TrySetResult();
                }
            })
            .Returns(Task.CompletedTask);
        return reached.Task;
    }

    private static Mock<IHubContext<HealthHub>> HubContextFor(Mock<IClientProxy> clientProxy)
    {
        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        return hubContext;
    }

    private static HealthReport BuildReport(HealthStatus status, string? name = null)
    {
        var serviceName = name ?? MonitoredServiceName;
        return new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                [serviceName] = new(status, Generated.NewMonitoredServiceDescription(), TimeSpan.Zero, null, null),
            },
            TimeSpan.Zero);
    }
}

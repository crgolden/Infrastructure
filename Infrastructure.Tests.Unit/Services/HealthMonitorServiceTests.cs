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

    public void Dispose() => _harness.Dispose();

    private static IOptions<MonitoringOptions> GetDefaultOptions() =>
        Options.Create(new MonitoringOptions { IntervalSeconds = 1 });

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

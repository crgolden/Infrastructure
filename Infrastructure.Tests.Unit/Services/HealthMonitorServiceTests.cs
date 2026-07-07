namespace Infrastructure.Tests.Unit.Services;

using Infrastructure.Hubs;
using Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models;
using Moq;

[Trait("Category", "Unit")]
public sealed class HealthMonitorServiceTests
{
    [Fact]
    public async Task ExecuteAsync_StoresSnapshotAfterFirstPoll()
    {
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
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
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await svc.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        Assert.NotNull(svc.LastSnapshot);
    }

    [Fact]
    public async Task ExecuteAsync_SendsAlertWhenServiceIsUnhealthyOnFirstPoll()
    {
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

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.Current.CancellationToken);
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

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(2500, TestContext.Current.CancellationToken);
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

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        alertService.Setup(a => a.SendRecoveryAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(3500, TestContext.Current.CancellationToken);
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
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
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
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        clientProxy.Verify(
            c => c.SendCoreAsync(
                "ReceiveSnapshot",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WhenIntervalSecondsIsNull_ThrowsInvalidOperationException()
    {
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var alertService = new Mock<IAlertService>(MockBehavior.Strict);

        Assert.Throws<InvalidOperationException>(() => new HealthMonitorService(
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            Options.Create(new MonitoringOptions { IntervalSeconds = null })));
    }

    [Fact]
    public async Task ExecuteAsync_WithDegradedService_MapsToDegradedStatus()
    {
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Degraded));

        var hubContext = new Mock<IHubContext<HealthHub>>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);

        var svc = new HealthMonitorService(
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await svc.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        Assert.NotNull(svc.LastSnapshot);
        Assert.Contains(svc.LastSnapshot.Results, r => r.Status == ServiceStatus.Degraded);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesPollingWhenCheckHealthAsyncThrows()
    {
        var healthCheckService = new Mock<HealthCheckService>(MockBehavior.Strict);
        healthCheckService
            .SetupSequence(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("downstream exploded"))
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
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        // CTS must outlive both polls: poll 1 throws at t=0, delay 1s, poll 2 succeeds at t=1s.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(2500, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        Assert.NotNull(svc.LastSnapshot);
        clientProxy.Verify(
            c => c.SendCoreAsync("ReceiveSnapshot", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesPollingWhenSignalRThrows()
    {
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

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.Current.CancellationToken);
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

        var alertService = new Mock<IAlertService>(MockBehavior.Strict);
        alertService.Setup(a => a.SendAlertAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));
        alertService.Setup(a => a.SendRecoveryAsync(It.IsAny<ServiceHealthResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new HealthMonitorService(
            NullLogger<HealthMonitorService>.Instance,
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(2500, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        alertService.Verify(
            a => a.SendRecoveryAsync(
                It.Is<ServiceHealthResult>(r => r.Status == ServiceStatus.Healthy),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private static IOptions<MonitoringOptions> GetDefaultOptions() =>
        Options.Create(new MonitoringOptions { IntervalSeconds = 1 });

    private static HealthReport BuildReport(HealthStatus status, string name = "SQL Server") =>
        new(
            new Dictionary<string, HealthReportEntry>
            {
                [name] = new(status, $"{status} description", TimeSpan.Zero, null, null),
            },
            TimeSpan.Zero);
}

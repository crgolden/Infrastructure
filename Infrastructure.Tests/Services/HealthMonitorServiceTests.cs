namespace Infrastructure.Tests.Services;

using Infrastructure.Hubs;
using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

[Trait("Category", "Unit")]
public sealed class HealthMonitorServiceTests
{
    [Fact]
    public async Task ExecuteAsync_StoresSnapshotAfterFirstPoll()
    {
        var healthCheckService = new Mock<HealthCheckService>();
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy));

        var hubContext = new Mock<IHubContext<HealthHub>>();
        var clients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);

        var alertService = new Mock<IAlertService>();

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            NullLogger<HealthMonitorService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await svc.StartAsync(cts.Token);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.NotNull(svc.LastSnapshot);
    }

    [Fact]
    public async Task ExecuteAsync_SendsAlertWhenServiceTransitionsToUnhealthy()
    {
        var callCount = 0;
        var healthCheckService = new Mock<HealthCheckService>();
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? BuildReport(HealthStatus.Healthy)
                    : BuildReport(HealthStatus.Unhealthy);
            });

        var hubContext = new Mock<IHubContext<HealthHub>>();
        var clients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);

        var alertService = new Mock<IAlertService>();

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            NullLogger<HealthMonitorService>.Instance);

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
        var callCount = 0;
        var healthCheckService = new Mock<HealthCheckService>();
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount switch
                {
                    1 => BuildReport(HealthStatus.Healthy),
                    2 => BuildReport(HealthStatus.Unhealthy),
                    _ => BuildReport(HealthStatus.Healthy),
                };
            });

        var hubContext = new Mock<IHubContext<HealthHub>>();
        var clients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);

        var alertService = new Mock<IAlertService>();

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            NullLogger<HealthMonitorService>.Instance);

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
        var healthCheckService = new Mock<HealthCheckService>();
        healthCheckService.Setup(h => h.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReport(HealthStatus.Healthy));

        var hubContext = new Mock<IHubContext<HealthHub>>();
        var clients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);

        var alertService = new Mock<IAlertService>();

        var svc = new HealthMonitorService(
            healthCheckService.Object,
            hubContext.Object,
            alertService.Object,
            GetDefaultOptions(),
            NullLogger<HealthMonitorService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        clientProxy.Verify(
            c => c.SendCoreAsync(
                "ReceiveSnapshot",
                It.IsAny<object[]>(),
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

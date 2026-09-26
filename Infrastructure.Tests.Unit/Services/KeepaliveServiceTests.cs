namespace Infrastructure.Tests.Unit.Services;

using System.Net.Http;
using Infrastructure.Services;
using Infrastructure.Tests.Unit.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

[Trait("Category", "Unit")]
public sealed class KeepaliveServiceTests
{
    private static readonly string PingHostname = Generated.NewHostname();

    [Fact]
    public async Task ExecuteAsync_WhenHostnameIsNull_ReturnsWithoutCallingHttp()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        using var handler = StubHttpMessageHandler.Recording();
        using var httpClient = handler.ToClient();
        var timeProvider = new FakeTimeProvider();
        var pingInterval = Generated.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        // Act
        await svc.StartAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(pingInterval);
        await svc.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.RequestedUris);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHostnameIsSet_CallsGetAsync()
    {
        // Arrange
        var config = ConfigurationWithHostname();
        using var handler = StubHttpMessageHandler.Recording();
        using var httpClient = handler.ToClient();
        var timeProvider = new TimerSignalingTimeProvider();
        var pingInterval = Generated.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        // Act
        await svc.StartAsync(TestContext.Current.CancellationToken);
        await timeProvider.FirstTimerCreated;
        timeProvider.Advance(pingInterval);
        await handler.FirstRequest;
        await svc.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(KeepaliveService.PingUri(PingHostname), handler.RequestedUris);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGetAsyncThrows_DoesNotPropagateException()
    {
        // Arrange
        var config = ConfigurationWithHostname();
        using var handler = StubHttpMessageHandler.RecordingAndThrowing(
            new HttpRequestException(Generated.NewTransportFailureMessage()));
        using var httpClient = handler.ToClient();
        var timeProvider = new TimerSignalingTimeProvider();
        var pingInterval = Generated.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        // Act
        await svc.StartAsync(TestContext.Current.CancellationToken);
        await timeProvider.FirstTimerCreated;
        timeProvider.Advance(pingInterval);
        await handler.FirstRequest;
        await svc.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var executeTask = svc.ExecuteTask;
        Assert.NotNull(executeTask);
        Assert.False(executeTask.IsFaulted);
    }

    private static IConfiguration ConfigurationWithHostname() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([
                new KeyValuePair<string, string?>(KeepaliveService.HostnameConfigurationKey, PingHostname)])
            .Build();
}

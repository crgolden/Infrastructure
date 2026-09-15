namespace Infrastructure.Tests.Unit.Services;

using System.Net.Http;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using TestSupport;

[Trait("Category", "Unit")]
public sealed class KeepaliveServiceTests
{
    private static readonly string PingHostname = TestValues.NewHostname();

    [Fact]
    public async Task ExecuteAsync_WhenHostnameIsNull_ReturnsWithoutCallingHttp()
    {
        var config = new ConfigurationBuilder().Build();
        using var handler = StubHttpMessageHandler.Recording();
        using var httpClient = handler.ToClient();
        var timeProvider = new FakeTimeProvider();
        var pingInterval = TestValues.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        await svc.StartAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(pingInterval);
        await svc.StopAsync(TestContext.Current.CancellationToken);

        Assert.Empty(handler.RequestedUris);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHostnameIsSet_CallsGetAsync()
    {
        var config = ConfigurationWithHostname();
        using var handler = StubHttpMessageHandler.Recording();
        using var httpClient = handler.ToClient();
        var timeProvider = new FakeTimeProvider();
        var pingInterval = TestValues.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        await svc.StartAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(pingInterval);
        await svc.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains(KeepaliveService.PingUri(PingHostname), handler.RequestedUris);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGetAsyncThrows_DoesNotPropagateException()
    {
        var config = ConfigurationWithHostname();
        using var handler = StubHttpMessageHandler.RecordingAndThrowing(
            new HttpRequestException(TestValues.NewTransportFailureMessage()));
        using var httpClient = handler.ToClient();
        var timeProvider = new FakeTimeProvider();
        var pingInterval = TestValues.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        await svc.StartAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(pingInterval);
        await svc.StopAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(handler.RequestedUris);
    }

    private static IConfiguration ConfigurationWithHostname() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([
                new KeyValuePair<string, string?>(KeepaliveService.HostnameConfigurationKey, PingHostname)])
            .Build();
}

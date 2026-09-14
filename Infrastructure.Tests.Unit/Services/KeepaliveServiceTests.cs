namespace Infrastructure.Tests.Unit.Services;

using System.Net;
using System.Net.Http;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Protected;
using TestSupport;

[Trait("Category", "Unit")]
public sealed class KeepaliveServiceTests
{
    private const string SendAsyncMethod = "SendAsync";

    private static readonly TimeSpan MissedPingSignalTimeout = TimeSpan.FromSeconds(30);

    private static readonly string PingHostname = TestValues.NewHostname();

    [Fact]
    public async Task ExecuteAsync_WhenHostnameIsNull_ReturnsWithoutCallingHttp()
    {
        var config = new ConfigurationBuilder().Build();
        var handlerMock = new Mock<HttpMessageHandler>();
        using var httpClient = new HttpClient(handlerMock.Object);
        var timeProvider = new FakeTimeProvider();
        var pingInterval = TestValues.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        await svc.StartAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(pingInterval);
        await svc.StopAsync(TestContext.Current.CancellationToken);

        handlerMock.Protected()
            .Verify(
                SendAsyncMethod,
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenHostnameIsSet_CallsGetAsync()
    {
        var config = ConfigurationWithHostname();
        var pinged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                SendAsyncMethod,
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                pinged.TrySetResult();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
        using var httpClient = new HttpClient(handlerMock.Object);
        var timeProvider = new FakeTimeProvider();
        var pingInterval = TestValues.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        await svc.StartAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(pingInterval);
        await pinged.Task.WaitAsync(MissedPingSignalTimeout, TestContext.Current.CancellationToken);
        await svc.StopAsync(TestContext.Current.CancellationToken);

        handlerMock.Protected()
            .Verify(
                SendAsyncMethod,
                Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == KeepaliveService.PingUri(PingHostname)),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenGetAsyncThrows_DoesNotPropagateException()
    {
        var config = ConfigurationWithHostname();
        var pinged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                SendAsyncMethod,
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                pinged.TrySetResult();
                return Task.FromException<HttpResponseMessage>(
                    new HttpRequestException(TestValues.NewTransportFailureMessage()));
            });
        using var httpClient = new HttpClient(handlerMock.Object);
        var timeProvider = new FakeTimeProvider();
        var pingInterval = TestValues.NewPingInterval();
        using var svc = new KeepaliveService(httpClient, config, pingInterval, timeProvider);

        await svc.StartAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(pingInterval);
        await pinged.Task.WaitAsync(MissedPingSignalTimeout, TestContext.Current.CancellationToken);
        await svc.StopAsync(TestContext.Current.CancellationToken);

        handlerMock.Protected()
            .Verify(
                SendAsyncMethod,
                Times.AtLeastOnce(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    private static IConfiguration ConfigurationWithHostname() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([
                new KeyValuePair<string, string?>(KeepaliveService.HostnameConfigurationKey, PingHostname)])
            .Build();
}

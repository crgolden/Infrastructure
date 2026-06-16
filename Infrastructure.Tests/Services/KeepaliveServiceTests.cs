namespace Infrastructure.Tests.Services;

using System.Net;
using System.Net.Http;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;

[Trait("Category", "Unit")]
public sealed class KeepaliveServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenHostnameIsNull_ReturnsWithoutCallingHttp()
    {
        var config = new ConfigurationBuilder().Build();
        var handlerMock = new Mock<HttpMessageHandler>();
        using var httpClient = new HttpClient(handlerMock.Object);
        var svc = new KeepaliveService(httpClient, config, TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await svc.StartAsync(cts.Token);

        handlerMock.Protected()
            .Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenHostnameIsSet_CallsGetAsync()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("WEBSITE_HOSTNAME", "example.com")])
            .Build();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handlerMock.Object);
        var svc = new KeepaliveService(httpClient, config, TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        handlerMock.Protected()
            .Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == new Uri("https://example.com/ping")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenGetAsyncThrows_DoesNotPropagateException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("WEBSITE_HOSTNAME", "example.com")])
            .Build();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));
        using var httpClient = new HttpClient(handlerMock.Object);
        var svc = new KeepaliveService(httpClient, config, TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        _ = svc.StartAsync(cts.Token);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        handlerMock.Protected()
            .Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }
}

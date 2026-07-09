namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;
using Moq;
using Moq.Protected;

[Trait("Category", "Unit")]
public sealed class UptimeKumaHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccess_ReturnsHealthy()
    {
        var check = new UptimeKumaHealthCheck(BuildClient(HttpStatusCode.OK), GetDefaultOptions());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Uptime Kuma", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsNotSuccess_ReturnsUnhealthy()
    {
        var check = new UptimeKumaHealthCheck(BuildClient(HttpStatusCode.InternalServerError), GetDefaultOptions());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Uptime Kuma", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsUnhealthy()
    {
        var check = new UptimeKumaHealthCheck(BuildThrowingClient(new HttpRequestException("timeout")), GetDefaultOptions());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Uptime Kuma", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("timeout", result.Description);
    }

    // URL is never contacted — the HttpMessageHandler is mocked to return a canned
    // response for any request. Uses the reserved .test TLD (RFC 6761) so the fixture
    // is permanently decoupled from real DNS/hostnames.
    private static IOptions<ServiceEndpointOptions> GetDefaultOptions() => Options.Create(new ServiceEndpointOptions { UptimeKuma = new Uri("https://uptime-kuma.test:3001") });

    private static HttpClient BuildClient(HttpStatusCode statusCode)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
        return new HttpClient(handler.Object);
    }

    private static HttpClient BuildThrowingClient(Exception ex)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(ex);
        return new HttpClient(handler.Object);
    }
}

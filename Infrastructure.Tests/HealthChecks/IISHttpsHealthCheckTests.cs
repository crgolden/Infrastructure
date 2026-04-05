namespace Infrastructure.Tests.HealthChecks;

using System.Net;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;
using Moq;
using Moq.Protected;

[Trait("Category", "Unit")]
public sealed class IISHttpsHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccess_ReturnsHealthy()
    {
        var check = new IisHttpsHealthCheck(BuildClient(HttpStatusCode.OK), GetDefaultOptions());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("IIS HTTPS", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsNotSuccess_ReturnsUnhealthy()
    {
        var check = new IisHttpsHealthCheck(BuildClient(HttpStatusCode.ServiceUnavailable), GetDefaultOptions());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("IIS HTTPS", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsUnhealthy()
    {
        var check = new IisHttpsHealthCheck(BuildThrowingClient(new HttpRequestException("connection refused")), GetDefaultOptions());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("IIS HTTPS", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("connection refused", result.Description);
    }

    private static IOptions<ServiceEndpointOptions> GetDefaultOptions() => Options.Create(new ServiceEndpointOptions { IisHttps = new Uri("https://localhost:443") });

    private static HttpClient BuildClient(HttpStatusCode statusCode)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
        return new HttpClient(handler.Object);
    }

    private static HttpClient BuildThrowingClient(Exception ex)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(ex);
        return new HttpClient(handler.Object);
    }
}

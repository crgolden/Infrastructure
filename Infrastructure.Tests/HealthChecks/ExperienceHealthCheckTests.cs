namespace Infrastructure.Tests.HealthChecks;

using System.Net;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Moq.Protected;

[Trait("Category", "Unit")]
public sealed class ExperienceHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccessAndBodyIsHealthy_ReturnsHealthy()
    {
        var check = new ExperienceHealthCheck(BuildClient(HttpStatusCode.OK, "Healthy"), GetDefaultConfiguration());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Experience", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccessButBodyIsNotHealthy_ReturnsUnhealthy()
    {
        var check = new ExperienceHealthCheck(BuildClient(HttpStatusCode.OK, "Degraded"), GetDefaultConfiguration());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Experience", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsNotSuccess_ReturnsUnhealthy()
    {
        var check = new ExperienceHealthCheck(BuildClient(HttpStatusCode.ServiceUnavailable, string.Empty), GetDefaultConfiguration());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Experience", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsUnhealthy()
    {
        var check = new ExperienceHealthCheck(BuildThrowingClient(new HttpRequestException("timeout")), GetDefaultConfiguration());
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Experience", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("timeout", result.Description);
    }

    private static IConfiguration GetDefaultConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ExperienceServerAddress"] = "https://crgolden-experience.azurewebsites.net" })
            .Build();

    private static HttpClient BuildClient(HttpStatusCode statusCode, string content)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode) { Content = new StringContent(content) });
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

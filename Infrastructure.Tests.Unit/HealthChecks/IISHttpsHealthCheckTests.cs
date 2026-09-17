namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;
using TestSupport;

[Trait("Category", "Unit")]
public sealed class IISHttpsHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccess_ReturnsHealthy()
    {
        // Arrange
        var check = new IisHttpsHealthCheck(BuildClient(HttpStatusCode.OK), GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.IisHttps);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsNotSuccess_ReturnsUnhealthy()
    {
        // Arrange
        var check = new IisHttpsHealthCheck(BuildClient(HttpStatusCode.ServiceUnavailable), GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.IisHttps);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsUnhealthy()
    {
        // Arrange
        var transportFailureMessage = TestValues.NewTransportFailureMessage();
        var check = new IisHttpsHealthCheck(BuildThrowingClient(new HttpRequestException(transportFailureMessage)), GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.IisHttps);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(transportFailureMessage, result.Description);
    }

    private static IOptions<ServiceEndpointOptions> GetDefaultOptions() => Options.Create(new ServiceEndpointOptions { IisHttps = new Uri(TestValues.NewServiceAddress()) });

    private static HttpClient BuildClient(HttpStatusCode statusCode) =>
        StubHttpMessageHandler.RespondingWith(statusCode, string.Empty);

    private static HttpClient BuildThrowingClient(Exception ex) =>
        StubHttpMessageHandler.Throwing(ex);
}
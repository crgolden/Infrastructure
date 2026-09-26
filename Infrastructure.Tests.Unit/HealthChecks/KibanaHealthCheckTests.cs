namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net;
using Infrastructure.HealthChecks;
using Infrastructure.Models;
using Infrastructure.Tests.Unit.TestSupport;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

[Trait("Category", "Unit")]
public sealed class KibanaHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccess_ReturnsHealthy()
    {
        // Arrange
        var check = new KibanaHealthCheck(BuildClient(HttpStatusCode.OK), GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Kibana);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenKibanaIsDown_ReturnsUnhealthy()
    {
        // Arrange
        var check = new KibanaHealthCheck(
            BuildThrowingClient(new HttpRequestException(Generated.NewTransportFailureMessage())),
            GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Kibana);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsNotSuccess_ReturnsUnhealthy()
    {
        // Arrange
        var check = new KibanaHealthCheck(BuildClient(HttpStatusCode.ServiceUnavailable), GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Kibana);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static IOptions<ServiceEndpointOptions> GetDefaultOptions() => Options.Create(new ServiceEndpointOptions { Kibana = new Uri(Generated.NewServiceAddress()) });

    private static HttpClient BuildClient(HttpStatusCode statusCode) =>
        StubHttpMessageHandler.RespondingWith(statusCode, string.Empty);

    private static HttpClient BuildThrowingClient(Exception ex) =>
        StubHttpMessageHandler.Throwing(ex);
}

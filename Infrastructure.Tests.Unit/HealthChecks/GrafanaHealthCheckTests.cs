namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;
using TestSupport;

[Trait("Category", "Unit")]
public sealed class GrafanaHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccess_ReturnsHealthy()
    {
        // Arrange
        var check = new GrafanaHealthCheck(BuildClient(HttpStatusCode.OK), GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Grafana);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsNotSuccess_ReturnsUnhealthy()
    {
        // Arrange
        var check = new GrafanaHealthCheck(BuildClient(HttpStatusCode.InternalServerError), GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Grafana);

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
        var check = new GrafanaHealthCheck(BuildThrowingClient(new HttpRequestException(transportFailureMessage)), GetDefaultOptions());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Grafana);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(transportFailureMessage, result.Description);
    }

    private static IOptions<ServiceEndpointOptions> GetDefaultOptions() => Options.Create(new ServiceEndpointOptions { Grafana = new Uri(TestValues.NewServiceAddress()) });

    private static HttpClient BuildClient(HttpStatusCode statusCode) =>
        StubHttpMessageHandler.RespondingWith(statusCode, string.Empty);

    private static HttpClient BuildThrowingClient(Exception ex) =>
        StubHttpMessageHandler.Throwing(ex);
}
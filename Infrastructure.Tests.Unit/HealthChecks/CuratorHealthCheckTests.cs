namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net;
using Infrastructure.HealthChecks;
using Infrastructure.Tests.Unit.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

[Trait("Category", "Unit")]
public sealed class CuratorHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccessAndBodyIsHealthy_ReturnsHealthy()
    {
        // Arrange
        var check = new CuratorHealthCheck(BuildClient(HttpStatusCode.OK, SiblingAppHealthCheck.HealthyBody), GetDefaultConfiguration());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Curator);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccessButBodyIsNotHealthy_ReturnsUnhealthy()
    {
        // Arrange
        var check = new CuratorHealthCheck(BuildClient(HttpStatusCode.OK, Generated.NewUnexpectedHealthBody()), GetDefaultConfiguration());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Curator);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsNotSuccess_ReturnsUnhealthy()
    {
        // Arrange
        var check = new CuratorHealthCheck(BuildClient(HttpStatusCode.ServiceUnavailable, string.Empty), GetDefaultConfiguration());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Curator);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsUnhealthy()
    {
        // Arrange
        var transportFailureMessage = Generated.NewTransportFailureMessage();
        var check = new CuratorHealthCheck(BuildThrowingClient(new HttpRequestException(transportFailureMessage)), GetDefaultConfiguration());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Curator);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(transportFailureMessage, result.Description);
    }

    private static IConfiguration GetDefaultConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [CuratorHealthCheck.ConfigurationKey] = Generated.NewServiceAddress() })
            .Build();

    private static HttpClient BuildClient(HttpStatusCode statusCode, string content) =>
        StubHttpMessageHandler.RespondingWith(statusCode, content);

    private static HttpClient BuildThrowingClient(Exception ex) =>
        StubHttpMessageHandler.Throwing(ex);
}

namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TestSupport;

[Trait("Category", "Unit")]
public sealed class DirectoryHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccessAndBodyIsHealthy_ReturnsHealthy()
    {
        var check = new DirectoryHealthCheck(BuildClient(HttpStatusCode.OK, SiblingAppHealthCheck.HealthyBody), GetDefaultConfiguration());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Directory);

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsSuccessButBodyIsNotHealthy_ReturnsUnhealthy()
    {
        var check = new DirectoryHealthCheck(BuildClient(HttpStatusCode.OK, TestValues.NewUnexpectedHealthBody()), GetDefaultConfiguration());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Directory);

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsNotSuccess_ReturnsUnhealthy()
    {
        var check = new DirectoryHealthCheck(BuildClient(HttpStatusCode.ServiceUnavailable, string.Empty), GetDefaultConfiguration());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Directory);

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsUnhealthy()
    {
        var transportFailureMessage = TestValues.NewTransportFailureMessage();
        var check = new DirectoryHealthCheck(BuildThrowingClient(new HttpRequestException(transportFailureMessage)), GetDefaultConfiguration());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Directory);

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(transportFailureMessage, result.Description);
    }

    private static IConfiguration GetDefaultConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DirectoryHealthCheck.ConfigurationKey] = TestValues.NewServiceAddress() })
            .Build();

    private static HttpClient BuildClient(HttpStatusCode statusCode, string content) =>
        StubHttpMessageHandler.RespondingWith(statusCode, content);

    private static HttpClient BuildThrowingClient(Exception ex) =>
        StubHttpMessageHandler.Throwing(ex);
}
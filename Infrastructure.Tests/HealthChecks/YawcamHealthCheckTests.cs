namespace Infrastructure.Tests.HealthChecks;

using System.Net.Sockets;
using Infrastructure.HealthChecks;
using Infrastructure.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;

[Trait("Category", "Unit")]
public sealed class YawcamHealthCheckTests
{
    private static IOptions<ServiceEndpointOptions> DefaultOptions => Options.Create(new ServiceEndpointOptions());

    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        Func<TcpClient> factory = () => throw new SocketException(10061);
        var check = new YawcamHealthCheck(factory, DefaultOptions);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Yawcam AI", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionRefused_ReturnsUnhealthy()
    {
        // Connect to a port that should be closed (use loopback with unusual port)
        var options = Options.Create(new ServiceEndpointOptions { YawcamHost = "127.0.0.1", YawcamPort = 19999 });
        Func<TcpClient> factory = () => new TcpClient();
        var check = new YawcamHealthCheck(factory, options);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Yawcam AI", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}

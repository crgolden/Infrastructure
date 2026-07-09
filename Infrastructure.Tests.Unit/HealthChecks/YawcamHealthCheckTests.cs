namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net.Sockets;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

[Trait("Category", "Unit")]
public sealed class YawcamHealthCheckTests
{
    private static IOptions<ServiceEndpointOptions> DefaultOptions => Options.Create(new ServiceEndpointOptions { YawcamHost = "127.0.0.1", YawcamPort = 5995 });

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

    [Fact]
    public void Constructor_WhenHostIsEmpty_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new ServiceEndpointOptions { YawcamHost = "", YawcamPort = 5995 });
        Assert.Throws<InvalidOperationException>(() => new YawcamHealthCheck(() => new TcpClient(), options));
    }

    [Fact]
    public void Constructor_WhenPortIsNull_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new ServiceEndpointOptions { YawcamHost = "localhost", YawcamPort = null });
        Assert.Throws<InvalidOperationException>(() => new YawcamHealthCheck(() => new TcpClient(), options));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionSucceeds_ReturnsHealthy()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var options = Options.Create(new ServiceEndpointOptions { YawcamHost = "127.0.0.1", YawcamPort = port });
            Func<TcpClient> factory = () => new TcpClient();
            var check = new YawcamHealthCheck(factory, options);
            var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Yawcam AI", check, null, null) };

            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            listener.Stop();
        }
    }
}

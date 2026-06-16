namespace Infrastructure.Tests.HealthChecks;

using System.Net.Sockets;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

[Trait("Category", "Unit")]
public sealed class WMSvcHealthCheckTests
{
    private static IOptions<ServiceEndpointOptions> DefaultOptions => Options.Create(new ServiceEndpointOptions { WmsvcHost = "127.0.0.1", WmsvcPort = 8172 });

    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        Func<TcpClient> factory = () => throw new SocketException(10061);
        var check = new WMSvcHealthCheck(factory, DefaultOptions);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("WMSvc", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionRefused_ReturnsUnhealthy()
    {
        var options = Options.Create(new ServiceEndpointOptions { WmsvcHost = "127.0.0.1", WmsvcPort = 19998 });
        Func<TcpClient> factory = () => new TcpClient();
        var check = new WMSvcHealthCheck(factory, options);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("WMSvc", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Constructor_WhenHostIsEmpty_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new ServiceEndpointOptions { WmsvcHost = "", WmsvcPort = 8172 });
        Assert.Throws<InvalidOperationException>(() => new WMSvcHealthCheck(() => new TcpClient(), options));
    }

    [Fact]
    public void Constructor_WhenPortIsNull_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new ServiceEndpointOptions { WmsvcHost = "localhost", WmsvcPort = null });
        Assert.Throws<InvalidOperationException>(() => new WMSvcHealthCheck(() => new TcpClient(), options));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionSucceeds_ReturnsHealthy()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var options = Options.Create(new ServiceEndpointOptions { WmsvcHost = "127.0.0.1", WmsvcPort = port });
            Func<TcpClient> factory = () => new TcpClient();
            var check = new WMSvcHealthCheck(factory, options);
            var context = new HealthCheckContext { Registration = new HealthCheckRegistration("WMSvc", check, null, null) };

            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            listener.Stop();
        }
    }
}

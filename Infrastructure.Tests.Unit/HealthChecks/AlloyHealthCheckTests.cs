namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net.Sockets;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

[Trait("Category", "Unit")]
public sealed class AlloyHealthCheckTests
{
    private static IOptions<ServiceEndpointOptions> DefaultOptions => Options.Create(new ServiceEndpointOptions { AlloyHost = "127.0.0.1", AlloyPort = 4317 });

    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        Func<TcpClient> factory = () => throw new SocketException(10061);
        var check = new AlloyHealthCheck(factory, DefaultOptions);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Alloy", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionRefused_ReturnsUnhealthy()
    {
        var options = Options.Create(new ServiceEndpointOptions { AlloyHost = "127.0.0.1", AlloyPort = 19997 });
        Func<TcpClient> factory = () => new TcpClient();
        var check = new AlloyHealthCheck(factory, options);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Alloy", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Constructor_WhenHostIsEmpty_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new ServiceEndpointOptions { AlloyHost = "", AlloyPort = 4317 });
        Assert.Throws<InvalidOperationException>(() => new AlloyHealthCheck(() => new TcpClient(), options));
    }

    [Fact]
    public void Constructor_WhenPortIsNull_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new ServiceEndpointOptions { AlloyHost = "localhost", AlloyPort = null });
        Assert.Throws<InvalidOperationException>(() => new AlloyHealthCheck(() => new TcpClient(), options));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionSucceeds_ReturnsHealthy()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var options = Options.Create(new ServiceEndpointOptions { AlloyHost = "127.0.0.1", AlloyPort = port });
            Func<TcpClient> factory = () => new TcpClient();
            var check = new AlloyHealthCheck(factory, options);
            var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Alloy", check, null, null) };

            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            listener.Stop();
        }
    }
}

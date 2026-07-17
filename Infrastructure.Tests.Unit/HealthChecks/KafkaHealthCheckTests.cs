namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net.Sockets;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

[Trait("Category", "Unit")]
public sealed class KafkaHealthCheckTests
{
    private static IOptions<ServiceEndpointOptions> DefaultOptions => Options.Create(new ServiceEndpointOptions { KafkaHost = "127.0.0.1", KafkaPort = 9093 });

    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        Func<TcpClient> factory = () => throw new SocketException(10061);
        var check = new KafkaHealthCheck(factory, DefaultOptions);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Kafka", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionRefused_ReturnsUnhealthy()
    {
        var options = Options.Create(new ServiceEndpointOptions { KafkaHost = "127.0.0.1", KafkaPort = 19998 });
        Func<TcpClient> factory = () => new TcpClient();
        var check = new KafkaHealthCheck(factory, options);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Kafka", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Constructor_WhenHostIsEmpty_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new ServiceEndpointOptions { KafkaHost = "", KafkaPort = 9093 });
        Assert.Throws<InvalidOperationException>(() => new KafkaHealthCheck(() => new TcpClient(), options));
    }

    [Fact]
    public void Constructor_WhenPortIsNull_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new ServiceEndpointOptions { KafkaHost = "localhost", KafkaPort = null });
        Assert.Throws<InvalidOperationException>(() => new KafkaHealthCheck(() => new TcpClient(), options));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionSucceeds_ReturnsHealthy()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var options = Options.Create(new ServiceEndpointOptions { KafkaHost = "127.0.0.1", KafkaPort = port });
            Func<TcpClient> factory = () => new TcpClient();
            var check = new KafkaHealthCheck(factory, options);
            var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Kafka", check, null, null) };

            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            listener.Stop();
        }
    }
}

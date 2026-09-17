namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Net.Sockets;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;
using TestSupport;

[Trait("Category", "Unit")]
public sealed class AlloyHealthCheckTests
{
    private static IOptions<ServiceEndpointOptions> DefaultOptions =>
        Options.Create(new ServiceEndpointOptions
        {
            AlloyHost = TestValues.LoopbackHost,
            AlloyPort = TestValues.NewClosedLoopbackPort(),
        });

    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        // Arrange
        Func<TcpClient> factory = () => throw new SocketException((int)SocketError.ConnectionRefused);
        var check = new AlloyHealthCheck(factory, DefaultOptions);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Alloy);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionRefused_ReturnsUnhealthy()
    {
        // Arrange
        var options = Options.Create(new ServiceEndpointOptions
        {
            AlloyHost = TestValues.LoopbackHost,
            AlloyPort = TestValues.NewClosedLoopbackPort(),
        });
        Func<TcpClient> factory = () => new TcpClient();
        var check = new AlloyHealthCheck(factory, options);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Alloy);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Constructor_WhenHostIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = Options.Create(new ServiceEndpointOptions
        {
            AlloyHost = null,
            AlloyPort = TestValues.NewClosedLoopbackPort(),
        });

        // Act
        var exception = Record.Exception(() => new AlloyHealthCheck(() => new TcpClient(), options));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Constructor_WhenPortIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = Options.Create(new ServiceEndpointOptions
        {
            AlloyHost = TestValues.LoopbackHost,
            AlloyPort = null,
        });

        // Act
        var exception = Record.Exception(() => new AlloyHealthCheck(() => new TcpClient(), options));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionSucceeds_ReturnsHealthy()
    {
        // Arrange
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var options = Options.Create(new ServiceEndpointOptions
            {
                AlloyHost = TestValues.LoopbackHost,
                AlloyPort = port,
            });
            Func<TcpClient> factory = () => new TcpClient();
            var check = new AlloyHealthCheck(factory, options);
            var context = HealthCheckContexts.Create(check, HealthCheckNames.Alloy);

            // Act
            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            listener.Stop();
        }
    }
}
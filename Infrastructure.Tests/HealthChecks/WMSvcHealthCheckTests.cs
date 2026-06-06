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
}

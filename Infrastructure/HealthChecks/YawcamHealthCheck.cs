namespace Infrastructure.HealthChecks;

using System.Net.Sockets;
using Infrastructure.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

public sealed class YawcamHealthCheck(Func<TcpClient> tcpClientFactory, IOptions<ServiceEndpointOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = tcpClientFactory();
            await client.ConnectAsync(options.Value.YawcamHost, options.Value.YawcamPort, cancellationToken);
            return HealthCheckResult.Healthy($"TCP connected to {options.Value.YawcamHost}:{options.Value.YawcamPort}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

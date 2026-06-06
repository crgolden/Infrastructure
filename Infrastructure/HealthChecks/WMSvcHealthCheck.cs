namespace Infrastructure.HealthChecks;

using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

public sealed class WMSvcHealthCheck : IHealthCheck
{
    private readonly Func<TcpClient> _tcpClientFactory;
    private readonly string _wmsvcHost;
    private readonly int _wmsvcPort;

    public WMSvcHealthCheck(Func<TcpClient> tcpClientFactory, IOptions<ServiceEndpointOptions> options)
    {
        _tcpClientFactory = tcpClientFactory;
        if (IsNullOrWhiteSpace(options.Value.WmsvcHost))
        {
            throw new InvalidOperationException($"Invalid '{nameof(options.Value.WmsvcHost)}'.");
        }

        _wmsvcHost = options.Value.WmsvcHost;
        if (!options.Value.WmsvcPort.HasValue)
        {
            throw new InvalidOperationException($"Invalid '{nameof(options.Value.WmsvcPort)}'.");
        }

        _wmsvcPort = options.Value.WmsvcPort.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _tcpClientFactory();
            await client.ConnectAsync(_wmsvcHost, _wmsvcPort, cancellationToken);
            return HealthCheckResult.Healthy($"TCP connected to {_wmsvcHost}:{_wmsvcPort}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

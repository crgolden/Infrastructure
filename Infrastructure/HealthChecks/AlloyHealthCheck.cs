namespace Infrastructure.HealthChecks;

using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

public sealed class AlloyHealthCheck : IHealthCheck
{
    private readonly Func<TcpClient> _tcpClientFactory;
    private readonly string _alloyHost;
    private readonly int _alloyPort;

    public AlloyHealthCheck(Func<TcpClient> tcpClientFactory, IOptions<ServiceEndpointOptions> options)
    {
        _tcpClientFactory = tcpClientFactory;
        if (IsNullOrWhiteSpace(options.Value.AlloyHost))
        {
            throw new InvalidOperationException($"Invalid '{nameof(options.Value.AlloyHost)}'.");
        }

        _alloyHost = options.Value.AlloyHost;
        if (!options.Value.AlloyPort.HasValue)
        {
            throw new InvalidOperationException($"Invalid '{nameof(options.Value.AlloyPort)}'.");
        }

        _alloyPort = options.Value.AlloyPort.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _tcpClientFactory();
            await client.ConnectAsync(_alloyHost, _alloyPort, cancellationToken);
            return HealthCheckResult.Healthy($"TCP connected to {_alloyHost}:{_alloyPort}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

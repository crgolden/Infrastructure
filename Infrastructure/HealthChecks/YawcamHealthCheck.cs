namespace Infrastructure.HealthChecks;

using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

public sealed class YawcamHealthCheck : IHealthCheck
{
    private readonly Func<TcpClient> _tcpClientFactory;
    private readonly string _yawcamHost;
    private readonly int _yawcamPort;

    public YawcamHealthCheck(Func<TcpClient> tcpClientFactory, IOptions<ServiceEndpointOptions> options)
    {
        _tcpClientFactory = tcpClientFactory;
        if (IsNullOrWhiteSpace(options.Value.YawcamHost))
        {
            throw new InvalidOperationException($"Invalid '{nameof(options.Value.YawcamHost)}'.");
        }

        _yawcamHost = options.Value.YawcamHost;
        if (!options.Value.YawcamPort.HasValue)
        {
            throw new InvalidOperationException($"Invalid '{nameof(options.Value.YawcamPort)}'.");
        }

        _yawcamPort = options.Value.YawcamPort.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _tcpClientFactory();
            await client.ConnectAsync(_yawcamHost, _yawcamPort, cancellationToken);
            return HealthCheckResult.Healthy($"TCP connected to {_yawcamHost}:{_yawcamPort}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

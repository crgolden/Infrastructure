namespace Infrastructure.HealthChecks;

using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

public sealed class KafkaHealthCheck : IHealthCheck
{
    private readonly Func<TcpClient> _tcpClientFactory;
    private readonly string _kafkaHost;
    private readonly int _kafkaPort;

    public KafkaHealthCheck(Func<TcpClient> tcpClientFactory, IOptions<ServiceEndpointOptions> options)
    {
        _tcpClientFactory = tcpClientFactory;
        if (IsNullOrWhiteSpace(options.Value.KafkaHost))
        {
            throw new InvalidOperationException($"Invalid '{nameof(options.Value.KafkaHost)}'.");
        }

        _kafkaHost = options.Value.KafkaHost;
        if (!options.Value.KafkaPort.HasValue)
        {
            throw new InvalidOperationException($"Invalid '{nameof(options.Value.KafkaPort)}'.");
        }

        _kafkaPort = options.Value.KafkaPort.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _tcpClientFactory();
            await client.ConnectAsync(_kafkaHost, _kafkaPort, cancellationToken);
            return HealthCheckResult.Healthy($"TCP connected to {_kafkaHost}:{_kafkaPort}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

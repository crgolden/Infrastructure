namespace Infrastructure.HealthChecks;

using Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

public sealed class UptimeKumaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly Uri _requestUri;

    public UptimeKumaHealthCheck(HttpClient httpClient, IOptions<ServiceEndpointOptions> options)
    {
        _httpClient = httpClient;
        _requestUri = options.Value.UptimeKuma ?? throw new InvalidOperationException($"Invalid '{nameof(options.Value.UptimeKuma)}'.");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(_requestUri, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"HTTP {(int)response.StatusCode}")
                : HealthCheckResult.Unhealthy($"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

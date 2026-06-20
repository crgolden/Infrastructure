namespace Infrastructure.HealthChecks;

using Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

public sealed class GrafanaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly Uri _requestUri;

    public GrafanaHealthCheck(HttpClient httpClient, IOptions<ServiceEndpointOptions> options)
    {
        _httpClient = httpClient;
        _requestUri = options.Value.Grafana ?? throw new InvalidOperationException($"Invalid '{nameof(options.Value.Grafana)}'.");
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

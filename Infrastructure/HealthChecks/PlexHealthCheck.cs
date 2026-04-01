namespace Infrastructure.HealthChecks;

using Infrastructure.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Http;
using Microsoft.Extensions.Options;

public sealed class PlexHealthCheck(IHttpClientFactory httpClientFactory, IOptions<ServiceEndpointOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("HealthCheck");
            var response = await client.GetAsync(options.Value.Plex, cancellationToken);
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

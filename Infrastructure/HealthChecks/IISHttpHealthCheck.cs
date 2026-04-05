namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Models;

public sealed class IisHttpHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly Uri _requestUri;

    public IisHttpHealthCheck(HttpClient httpClient, IOptions<ServiceEndpointOptions> options)
    {
        _httpClient = httpClient;
        _requestUri = options.Value.IisHttp ?? throw new InvalidOperationException($"Invalid '{nameof(options.Value.IisHttp)}'.");
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

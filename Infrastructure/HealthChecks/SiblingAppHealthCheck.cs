namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public abstract class SiblingAppHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly Uri _requestUri;

    protected SiblingAppHealthCheck(HttpClient httpClient, IConfiguration configuration, string configKey)
    {
        _httpClient = httpClient;
        var baseUri = configuration.GetValue<Uri?>(configKey)
            ?? throw new InvalidOperationException($"Invalid '{configKey}'.");
        _requestUri = new Uri(baseUri, "/health");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(_requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy($"HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Trim() == "Healthy"
                ? HealthCheckResult.Healthy($"HTTP {(int)response.StatusCode}")
                : HealthCheckResult.Unhealthy($"Unexpected response: {body}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

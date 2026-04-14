namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class ProductsHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly Uri _requestUri;

    public ProductsHealthCheck(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var baseUri = configuration.GetValue<Uri?>("ProductsApiAddress") ?? throw new InvalidOperationException("Invalid 'ProductsApiAddress'.");
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

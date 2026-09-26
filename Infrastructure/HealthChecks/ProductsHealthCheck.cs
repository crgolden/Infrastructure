namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class ProductsHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, ConfigurationKey)
{
    internal const string ConfigurationKey = "ProductsApiAddress";
}

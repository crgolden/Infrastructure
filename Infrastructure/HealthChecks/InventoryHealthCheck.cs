namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class InventoryHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, ConfigurationKey)
{
    internal const string ConfigurationKey = "InventoryServerAddress";
}

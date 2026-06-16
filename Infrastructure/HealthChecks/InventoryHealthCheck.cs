namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class InventoryHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, "InventoryServerAddress")
{
}

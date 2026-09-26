namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class ManualsHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, ConfigurationKey)
{
    internal const string ConfigurationKey = "ManualsApiAddress";
}

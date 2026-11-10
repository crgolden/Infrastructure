namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class SentinelHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, ConfigurationKey)
{
    internal const string ConfigurationKey = "SentinelServerAddress";
}

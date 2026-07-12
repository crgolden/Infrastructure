namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class CuratorHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, "CuratorApiAddress")
{
}

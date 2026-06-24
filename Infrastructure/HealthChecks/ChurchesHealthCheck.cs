namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class ChurchesHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, "ChurchesServerAddress")
{
}

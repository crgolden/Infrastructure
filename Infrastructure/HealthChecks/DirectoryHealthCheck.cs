namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class DirectoryHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, "DirectoryApiAddress")
{
}

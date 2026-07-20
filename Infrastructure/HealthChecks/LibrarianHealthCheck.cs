namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class LibrarianHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, "LibrarianServerAddress")
{
}

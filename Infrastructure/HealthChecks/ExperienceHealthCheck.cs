namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class ExperienceHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, "ExperienceServerAddress");

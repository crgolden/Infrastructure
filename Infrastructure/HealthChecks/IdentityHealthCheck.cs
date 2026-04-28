namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Configuration;

public sealed class IdentityHealthCheck(HttpClient httpClient, IConfiguration configuration)
    : SiblingAppHealthCheck(httpClient, configuration, "OidcAuthority");

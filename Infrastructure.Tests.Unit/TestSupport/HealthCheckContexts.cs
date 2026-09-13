namespace Infrastructure.Tests.Unit.TestSupport;

using Microsoft.Extensions.Diagnostics.HealthChecks;

internal static class HealthCheckContexts
{
    internal static HealthCheckContext Create(IHealthCheck check, string registrationName) =>
        new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(registrationName, check, failureStatus: null, tags: null),
        };
}

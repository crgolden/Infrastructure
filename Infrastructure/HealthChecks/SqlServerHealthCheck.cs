namespace Infrastructure.HealthChecks;

using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly Func<IDbConnection> _connectionFactory;

    public SqlServerHealthCheck(Func<IDbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.ExecuteScalar();
            return HealthCheckResult.Healthy("Connected");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

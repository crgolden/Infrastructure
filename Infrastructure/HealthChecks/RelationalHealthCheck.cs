namespace Infrastructure.HealthChecks;

using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public abstract class RelationalHealthCheck : IHealthCheck
{
    private const int MaxAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly Func<IDbConnection> _connectionFactory;

    protected RelationalHealthCheck(Func<IDbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Exception lastException;
        var attempt = 0;
        do
        {
            attempt++;
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
                lastException = ex;
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }
        }
        while (attempt < MaxAttempts);

        return HealthCheckResult.Unhealthy(lastException.Message, lastException);
    }
}

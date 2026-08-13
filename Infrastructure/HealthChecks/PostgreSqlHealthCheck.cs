namespace Infrastructure.HealthChecks;

using System.Data;
using Microsoft.Extensions.DependencyInjection;

public sealed class PostgreSqlHealthCheck : RelationalHealthCheck
{
    public PostgreSqlHealthCheck([FromKeyedServices("PostgreSql")] Func<IDbConnection> connectionFactory)
        : base(connectionFactory)
    {
    }
}

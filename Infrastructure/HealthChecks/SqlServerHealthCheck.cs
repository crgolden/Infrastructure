namespace Infrastructure.HealthChecks;

using System.Data;
using Microsoft.Extensions.DependencyInjection;

public sealed class SqlServerHealthCheck : RelationalHealthCheck
{
    public SqlServerHealthCheck([FromKeyedServices("SqlServer")] Func<IDbConnection> connectionFactory)
        : base(connectionFactory)
    {
    }
}

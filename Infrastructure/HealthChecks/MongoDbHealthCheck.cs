namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

public sealed class MongoDbHealthCheck(IMongoClient mongoClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = mongoClient.GetDatabase("admin");
            var command = new BsonDocumentCommand<BsonDocument>(new BsonDocument("ping", 1));
            await db.RunCommandAsync(command, cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("Ping OK");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

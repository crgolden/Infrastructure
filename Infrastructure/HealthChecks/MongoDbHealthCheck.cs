namespace Infrastructure.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

public sealed class MongoDbHealthCheck : IHealthCheck
{
    internal const string DatabaseName = "crgolden";

    internal const string PingCommandName = "ping";

    internal const string CommandOkField = "ok";

    internal const string HealthyDescription = "Ping OK";

    private readonly IMongoClient _mongoClient;

    public MongoDbHealthCheck(IMongoClient mongoClient)
    {
        _mongoClient = mongoClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _mongoClient.GetDatabase(DatabaseName);
            var document = new BsonDocument(PingCommandName, 1);
            var command = new BsonDocumentCommand<BsonDocument>(document);
            await db.RunCommandAsync(command, cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy(HealthyDescription);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}

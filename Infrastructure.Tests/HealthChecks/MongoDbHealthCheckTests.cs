namespace Infrastructure.Tests.HealthChecks;

using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

[Trait("Category", "Unit")]
public sealed class MongoDbHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenPingSucceeds_ReturnsHealthy()
    {
        var db = new Mock<IMongoDatabase>();
        db.Setup(d => d.RunCommandAsync(
                It.IsAny<BsonDocumentCommand<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument("ok", 1));

        var client = new Mock<IMongoClient>();
        client.Setup(c => c.GetDatabase("crgolden", It.IsAny<MongoDatabaseSettings>())).Returns(db.Object);

        var check = new MongoDbHealthCheck(client.Object);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("MongoDB", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingThrows_ReturnsUnhealthy()
    {
        var db = new Mock<IMongoDatabase>();
        db.Setup(d => d.RunCommandAsync(
                It.IsAny<BsonDocumentCommand<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoConnectionException(new MongoDB.Driver.Core.Connections.ConnectionId(new MongoDB.Driver.Core.Servers.ServerId(new MongoDB.Driver.Core.Clusters.ClusterId(), new System.Net.DnsEndPoint("localhost", 27017))), "timeout"));

        var client = new Mock<IMongoClient>();
        client.Setup(c => c.GetDatabase("crgolden", It.IsAny<MongoDatabaseSettings>())).Returns(db.Object);

        var check = new MongoDbHealthCheck(client.Object);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("MongoDB", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenGetDatabaseThrows_ReturnsUnhealthy()
    {
        var client = new Mock<IMongoClient>();
        client.Setup(c => c.GetDatabase("crgolden", It.IsAny<MongoDatabaseSettings>()))
            .Throws(new InvalidOperationException("not connected"));

        var check = new MongoDbHealthCheck(client.Object);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("MongoDB", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}

namespace Infrastructure.Tests.Unit.HealthChecks;

using Infrastructure.HealthChecks;
using Infrastructure.Tests.Unit.TestSupport;
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
        // Arrange
        var db = new Mock<IMongoDatabase>(MockBehavior.Strict);
        db.Setup(d => d.RunCommandAsync(
                It.IsAny<BsonDocumentCommand<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument(MongoDbHealthCheck.CommandOkField, 1));

        var client = new Mock<IMongoClient>(MockBehavior.Strict);
        client.Setup(c => c.GetDatabase(MongoDbHealthCheck.DatabaseName, It.IsAny<MongoDatabaseSettings>())).Returns(db.Object);

        var check = new MongoDbHealthCheck(client.Object);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.MongoDb);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingThrows_ReturnsUnhealthy()
    {
        // Arrange
        var db = new Mock<IMongoDatabase>(MockBehavior.Strict);
        db.Setup(d => d.RunCommandAsync(
                It.IsAny<BsonDocumentCommand<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoConnectionException(
                new MongoDB.Driver.Core.Connections.ConnectionId(
                    new MongoDB.Driver.Core.Servers.ServerId(
                        new MongoDB.Driver.Core.Clusters.ClusterId(),
                        new System.Net.DnsEndPoint(Generated.LoopbackHost, Generated.NewClosedLoopbackPort()))),
                Generated.NewFailureMessage()));

        var client = new Mock<IMongoClient>(MockBehavior.Strict);
        client.Setup(c => c.GetDatabase(MongoDbHealthCheck.DatabaseName, It.IsAny<MongoDatabaseSettings>())).Returns(db.Object);

        var check = new MongoDbHealthCheck(client.Object);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.MongoDb);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenGetDatabaseThrows_ReturnsUnhealthy()
    {
        // Arrange
        var client = new Mock<IMongoClient>(MockBehavior.Strict);
        client.Setup(c => c.GetDatabase(MongoDbHealthCheck.DatabaseName, It.IsAny<MongoDatabaseSettings>()))
            .Throws(new InvalidOperationException("not connected"));

        var check = new MongoDbHealthCheck(client.Object);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.MongoDb);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}

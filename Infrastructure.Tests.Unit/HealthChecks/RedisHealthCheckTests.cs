namespace Infrastructure.Tests.Unit.HealthChecks;

using Infrastructure.HealthChecks;
using Infrastructure.Tests.Unit.TestSupport;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

[Trait("Category", "Unit")]
public sealed class RedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenPingSucceeds_ReturnsHealthy()
    {
        // Arrange
        var db = new Mock<IDatabase>(MockBehavior.Strict);
        db.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var muxer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        muxer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var check = new RedisHealthCheck(muxer.Object);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Redis);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingThrows_ReturnsUnhealthy()
    {
        // Arrange
        var db = new Mock<IDatabase>(MockBehavior.Strict);
        db.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, CommandFlags.None, Generated.NewFailureMessage()));
        var muxer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        muxer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var check = new RedisHealthCheck(muxer.Object);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Redis);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenGetDatabaseThrows_ReturnsUnhealthy()
    {
        // Arrange
        var muxer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        muxer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new InvalidOperationException("muxer not connected"));

        var check = new RedisHealthCheck(muxer.Object);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.Redis);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}

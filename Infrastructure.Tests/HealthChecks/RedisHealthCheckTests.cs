namespace Infrastructure.Tests.HealthChecks;

using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

[Trait("Category", "Unit")]
public sealed class RedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenPingSucceeds_ReturnsHealthy()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var muxer = new Mock<IConnectionMultiplexer>();
        muxer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var check = new RedisHealthCheck(muxer.Object);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Redis", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingThrows_ReturnsUnhealthy()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "refused"));
        var muxer = new Mock<IConnectionMultiplexer>();
        muxer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var check = new RedisHealthCheck(muxer.Object);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Redis", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenGetDatabaseThrows_ReturnsUnhealthy()
    {
        var muxer = new Mock<IConnectionMultiplexer>();
        muxer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new InvalidOperationException("muxer not connected"));

        var check = new RedisHealthCheck(muxer.Object);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("Redis", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}

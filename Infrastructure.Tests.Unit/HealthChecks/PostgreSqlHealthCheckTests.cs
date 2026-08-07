namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Data;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

[Trait("Category", "Unit")]
public sealed class PostgreSqlHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthyWithExceptionMessage()
    {
        var expected = new InvalidOperationException("connection failed");
        Func<IDbConnection> factory = () => throw expected;
        var check = new PostgreSqlHealthCheck(factory);

        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("connection failed", result.Description);
        Assert.Same(expected, result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOpenThrowsOnEveryAttempt_ReturnsUnhealthyAfterRetryingOnce()
    {
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open()).Throws(new InvalidOperationException("server unreachable"));
        mockConnection.Setup(c => c.Dispose());
        var check = new PostgreSqlHealthCheck(() => mockConnection.Object);

        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("server unreachable", result.Description);
        mockConnection.Verify(c => c.Open(), Times.Exactly(2));
        mockConnection.Verify(c => c.Dispose(), Times.Exactly(2));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExecuteScalarThrowsOnEveryAttempt_ReturnsUnhealthyAfterRetryingOnce()
    {
        var mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
        mockCommand.SetupSet(c => c.CommandText = "SELECT 1");
        mockCommand.Setup(c => c.ExecuteScalar()).Throws(new InvalidOperationException("permission denied"));
        mockCommand.Setup(c => c.Dispose());
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockConnection.Setup(c => c.Dispose());
        var check = new PostgreSqlHealthCheck(() => mockConnection.Object);

        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("permission denied", result.Description);
        mockCommand.Verify(c => c.Dispose(), Times.Exactly(2));
        mockConnection.Verify(c => c.Dispose(), Times.Exactly(2));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenFirstAttemptThrowsAndSecondSucceeds_ReturnsHealthy()
    {
        var attempt = 0;
        var check = new PostgreSqlHealthCheck(() =>
        {
            attempt++;
            if (attempt == 1)
            {
                throw new InvalidOperationException("transient network blip");
            }

            var mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
            mockCommand.SetupSet(c => c.CommandText = "SELECT 1");
            mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);
            mockCommand.Setup(c => c.Dispose());
            var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
            mockConnection.Setup(c => c.Open());
            mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
            mockConnection.Setup(c => c.Dispose());
            return mockConnection.Object;
        });

        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Connected", result.Description);
        Assert.Equal(2, attempt);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenQuerySucceeds_ReturnsHealthy()
    {
        var mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
        mockCommand.SetupSet(c => c.CommandText = "SELECT 1");
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);
        mockCommand.Setup(c => c.Dispose());
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockConnection.Setup(c => c.Dispose());
        var check = new PostgreSqlHealthCheck(() => mockConnection.Object);

        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Connected", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenQuerySucceeds_OpensConnectionAndIssuesSelect1()
    {
        var mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
        mockCommand.SetupSet(c => c.CommandText = "SELECT 1");
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);
        mockCommand.Setup(c => c.Dispose());
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockConnection.Setup(c => c.Dispose());
        var check = new PostgreSqlHealthCheck(() => mockConnection.Object);

        await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        mockConnection.Verify(c => c.Open(), Times.Once);
        mockCommand.VerifySet(c => c.CommandText = "SELECT 1", Times.Once);
        mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);
    }

    private static HealthCheckContext CreateContext(PostgreSqlHealthCheck check)
    {
        return new HealthCheckContext { Registration = new HealthCheckRegistration("PostgreSQL", check, null, null) };
    }
}
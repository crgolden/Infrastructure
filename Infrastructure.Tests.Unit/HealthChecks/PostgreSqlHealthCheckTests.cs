namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Data;
using Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using TestSupport;

[Trait("Category", "Unit")]
public sealed class PostgreSqlHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthyWithExceptionMessage()
    {
        // Arrange
        var failureMessage = TestValues.NewFailureMessage();
        var expected = new InvalidOperationException(failureMessage);
        Func<IDbConnection> factory = () => throw expected;
        var check = new PostgreSqlHealthCheck(factory);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(failureMessage, result.Description);
        Assert.Same(expected, result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOpenThrowsOnEveryAttempt_ReturnsUnhealthyAfterRetryingOnce()
    {
        // Arrange
        var failureMessage = TestValues.NewFailureMessage();
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open()).Throws(new InvalidOperationException(failureMessage));
        mockConnection.Setup(c => c.Dispose());
        var check = new PostgreSqlHealthCheck(() => mockConnection.Object);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(failureMessage, result.Description);
        mockConnection.Verify(c => c.Open(), Times.Exactly(RelationalHealthCheck.MaxAttempts));
        mockConnection.Verify(c => c.Dispose(), Times.Exactly(RelationalHealthCheck.MaxAttempts));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExecuteScalarThrowsOnEveryAttempt_ReturnsUnhealthyAfterRetryingOnce()
    {
        // Arrange
        var mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
        mockCommand.SetupSet(c => c.CommandText = RelationalHealthCheck.ProbeCommandText);
        var failureMessage = TestValues.NewFailureMessage();
        mockCommand.Setup(c => c.ExecuteScalar()).Throws(new InvalidOperationException(failureMessage));
        mockCommand.Setup(c => c.Dispose());
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockConnection.Setup(c => c.Dispose());
        var check = new PostgreSqlHealthCheck(() => mockConnection.Object);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(failureMessage, result.Description);
        mockCommand.Verify(c => c.Dispose(), Times.Exactly(RelationalHealthCheck.MaxAttempts));
        mockConnection.Verify(c => c.Dispose(), Times.Exactly(RelationalHealthCheck.MaxAttempts));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenFirstAttemptThrowsAndSecondSucceeds_ReturnsHealthy()
    {
        // Arrange
        var transientFailureMessage = $"transient-{Guid.NewGuid()}";
        var remainingAttempts = new Queue<Func<IDbConnection>>(
        [
            () => throw new InvalidOperationException(transientFailureMessage),
            BuildHealthyConnection,
        ]);
        var check = new PostgreSqlHealthCheck(() => remainingAttempts.Dequeue()());

        // Act
        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(RelationalHealthCheck.HealthyDescription, result.Description);
        Assert.Empty(remainingAttempts);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenQuerySucceeds_ReturnsHealthy()
    {
        // Arrange
        var mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
        mockCommand.SetupSet(c => c.CommandText = RelationalHealthCheck.ProbeCommandText);
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);
        mockCommand.Setup(c => c.Dispose());
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockConnection.Setup(c => c.Dispose());
        var check = new PostgreSqlHealthCheck(() => mockConnection.Object);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(RelationalHealthCheck.HealthyDescription, result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenQuerySucceeds_OpensConnectionAndIssuesSelect1()
    {
        // Arrange
        var mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
        mockCommand.SetupSet(c => c.CommandText = RelationalHealthCheck.ProbeCommandText);
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);
        mockCommand.Setup(c => c.Dispose());
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockConnection.Setup(c => c.Dispose());
        var check = new PostgreSqlHealthCheck(() => mockConnection.Object);

        // Act
        await check.CheckHealthAsync(CreateContext(check), CancellationToken.None);

        // Assert
        mockConnection.Verify(c => c.Open(), Times.Once);
        mockCommand.VerifySet(c => c.CommandText = RelationalHealthCheck.ProbeCommandText, Times.Once);
        mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);
    }

    private static HealthCheckContext CreateContext(PostgreSqlHealthCheck check)
    {
        return HealthCheckContexts.Create(check, HealthCheckNames.PostgreSql);
    }

    private static IDbConnection BuildHealthyConnection()
    {
        var mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
        mockCommand.SetupSet(c => c.CommandText = RelationalHealthCheck.ProbeCommandText);
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);
        mockCommand.Setup(c => c.Dispose());
        var mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockConnection.Setup(c => c.Dispose());
        return mockConnection.Object;
    }
}
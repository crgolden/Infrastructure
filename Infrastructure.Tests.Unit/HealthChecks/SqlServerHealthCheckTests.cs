namespace Infrastructure.Tests.Unit.HealthChecks;

using System.Data;
using Infrastructure.HealthChecks;
using Infrastructure.Tests.Unit.TestSupport;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

[Trait("Category", "Unit")]
public sealed class SqlServerHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        // Arrange
        var failureMessage = Generated.NewFailureMessage();
        Func<SqlConnection> factory = () => throw new InvalidOperationException(failureMessage);
        var check = new SqlServerHealthCheck(factory);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.SqlServer);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(failureMessage, result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionStringIsInvalid_ReturnsUnhealthy()
    {
        // Arrange
        Func<SqlConnection> factory = () => new SqlConnection(UnreachableSqlConnectionString());
        var check = new SqlServerHealthCheck(factory);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.SqlServer);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenQuerySucceeds_ReturnsHealthy()
    {
        // Arrange
        var mockCmd = new Mock<IDbCommand>();
        mockCmd.SetupSet(c => c.CommandText = It.IsAny<string>());
        mockCmd.Setup(c => c.ExecuteScalar()).Returns(1);
        var mockConn = new Mock<IDbConnection>();
        mockConn.Setup(c => c.Open());
        mockConn.Setup(c => c.CreateCommand()).Returns(mockCmd.Object);
        Func<IDbConnection> factory = () => mockConn.Object;
        var check = new SqlServerHealthCheck(factory);
        var context = HealthCheckContexts.Create(check, HealthCheckNames.SqlServer);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(RelationalHealthCheck.HealthyDescription, result.Description);
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
        var check = new SqlServerHealthCheck(() => remainingAttempts.Dequeue()());
        var context = HealthCheckContexts.Create(check, HealthCheckNames.SqlServer);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(RelationalHealthCheck.HealthyDescription, result.Description);
        Assert.Empty(remainingAttempts);
    }

    private static string UnreachableSqlConnectionString() =>
        new SqlConnectionStringBuilder
        {
            DataSource = $"{Generated.LoopbackHost},{Generated.NewClosedLoopbackPort()}",
            InitialCatalog = Generated.NewDatabaseName(),
            UserID = Generated.NewSqlLogin(),
            Password = Generated.NewPassword(),
            ConnectTimeout = 1,
            Encrypt = false,
        }.ConnectionString;

    private static IDbConnection BuildHealthyConnection()
    {
        var mockCmd = new Mock<IDbCommand>();
        mockCmd.SetupSet(c => c.CommandText = It.IsAny<string>());
        mockCmd.Setup(c => c.ExecuteScalar()).Returns(1);
        var mockConn = new Mock<IDbConnection>();
        mockConn.Setup(c => c.Open());
        mockConn.Setup(c => c.CreateCommand()).Returns(mockCmd.Object);
        return mockConn.Object;
    }
}

namespace Infrastructure.Tests.HealthChecks;

using System.Data;
using Infrastructure.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

[Trait("Category", "Unit")]
public sealed class SqlServerHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        Func<SqlConnection> factory = () => throw new InvalidOperationException("connection failed");
        var check = new SqlServerHealthCheck(factory);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("SQL Server", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("connection failed", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionStringIsInvalid_ReturnsUnhealthy()
    {
        // Using a valid-format but unreachable connection string to exercise the failure path
        Func<SqlConnection> factory = () => new SqlConnection("Server=127.0.0.1,9999;Database=test;User Id=sa;Password=wrong;Connect Timeout=1;Encrypt=False;");
        var check = new SqlServerHealthCheck(factory);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("SQL Server", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenQuerySucceeds_ReturnsHealthy()
    {
        var mockCmd = new Mock<IDbCommand>();
        mockCmd.SetupSet(c => c.CommandText = It.IsAny<string>());
        mockCmd.Setup(c => c.ExecuteScalar()).Returns(1);
        var mockConn = new Mock<IDbConnection>();
        mockConn.Setup(c => c.Open());
        mockConn.Setup(c => c.CreateCommand()).Returns(mockCmd.Object);
        Func<IDbConnection> factory = () => mockConn.Object;
        var check = new SqlServerHealthCheck(factory);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("SQL Server", check, null, null) };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Connected", result.Description);
    }
}

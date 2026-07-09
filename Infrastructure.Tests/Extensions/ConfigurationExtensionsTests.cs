namespace Infrastructure.Tests.Extensions;

using Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;

[Trait("Category", "Unit")]
public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void GetRequired_ReturnsValue_WhenKeyExists()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Foo"] = "bar" })
            .Build();

        Assert.Equal("bar", config.GetRequired<string>("Foo"));
    }

    [Fact]
    public void GetRequired_ThrowsInvalidOperationException_WhenKeyMissing()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => config.GetRequired<string>("Missing"));
    }

    [Fact]
    public void GetInfrastructureSecrets_ReturnsAllFourteenCredentials()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ElasticsearchUsername"] = "es-user",
                ["ElasticsearchPassword"] = "es-pass",
                ["ResendApiToken"] = "resend-token",
                ["SqlServerUserId"] = "sql-user",
                ["SqlServerPassword"] = "sql-pass",
                ["RedisPassword"] = "redis-pass",
                ["MongoDbUsername"] = "mongo-user",
                ["MongoDbPassword"] = "mongo-pass",
                ["PostgreSqlUserId"] = "postgres-user",
                ["PostgreSqlPassword"] = "postgres-pass",
                ["AdminEmail"] = "admin@example.com",
                ["ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v",
                ["InfrastructureClientId"] = "client-id",
                ["InfrastructureClientSecret"] = "client-secret",
            })
            .Build();

        var secrets = config.GetInfrastructureSecrets();

        Assert.Equal("es-user", secrets.ElasticsearchUsername);
        Assert.Equal("es-pass", secrets.ElasticsearchPassword);
        Assert.Equal("resend-token", secrets.ResendApiToken);
        Assert.Equal("sql-user", secrets.SqlServerUserId);
        Assert.Equal("sql-pass", secrets.SqlServerPassword);
        Assert.Equal("redis-pass", secrets.RedisPassword);
        Assert.Equal("mongo-user", secrets.MongoDbUsername);
        Assert.Equal("mongo-pass", secrets.MongoDbPassword);
        Assert.Equal("postgres-user", secrets.PostgreSqlUserId);
        Assert.Equal("postgres-pass", secrets.PostgreSqlPassword);
        Assert.Equal("admin@example.com", secrets.AdminEmail);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v", secrets.ServiceBusConnectionString);
        Assert.Equal("client-id", secrets.InfrastructureClientId);
        Assert.Equal("client-secret", secrets.InfrastructureClientSecret);
    }
}

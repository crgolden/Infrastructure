namespace Infrastructure.Tests.Unit.Extensions;

using Azure;
using Azure.Security.KeyVault.Secrets;
using Infrastructure.Extensions;
using Moq;

[Trait("Category", "Unit")]
public sealed class SecretClientExtensionsTests
{
    [Fact]
    public void GetInfrastructureSecrets_ReturnsTupleWithAllElevenSecretValues()
    {
        var values = new Dictionary<string, string>
        {
            ["ElasticsearchUsername"] = "es-user",
            ["ElasticsearchPassword"] = "es-pass",
            ["ResendApiToken"] = "resend-token",
            ["MasterSqlServerUserId"] = "sql-user",
            ["MasterSqlServerPassword"] = "sql-pass",
            ["RedisPassword"] = "redis-pass",
            ["MongoDbUsername"] = "mongo-user",
            ["MongoDbPassword"] = "mongo-pass",
            ["AdminEmail"] = "admin@example.com",
            ["InfrastructureClientId"] = "client-id",
            ["InfrastructureClientSecret"] = "client-secret",
        };
        var mock = new Mock<SecretClient>();
        mock.Setup(c => c.GetSecret(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .Returns<string, string?, SecretContentType?, CancellationToken>((name, _, _, _) => SecretResponse(name, values[name]));

        var secrets = mock.Object.GetInfrastructureSecrets();

        Assert.Equal("es-user", secrets.ElasticsearchUsername.Value);
        Assert.Equal("es-pass", secrets.ElasticsearchPassword.Value);
        Assert.Equal("resend-token", secrets.ResendApiToken.Value);
        Assert.Equal("sql-user", secrets.SqlServerUserId.Value);
        Assert.Equal("sql-pass", secrets.SqlServerPassword.Value);
        Assert.Equal("redis-pass", secrets.RedisPassword.Value);
        Assert.Equal("mongo-user", secrets.MongoDbUsername.Value);
        Assert.Equal("mongo-pass", secrets.MongoDbPassword.Value);
        Assert.Equal("admin@example.com", secrets.AdminEmail.Value);
        Assert.Equal("client-id", secrets.InfrastructureClientId.Value);
        Assert.Equal("client-secret", secrets.InfrastructureClientSecret.Value);
    }

    private static Response<KeyVaultSecret> SecretResponse(string name, string value) =>
        Response.FromValue(new KeyVaultSecret(name, value), Mock.Of<Response>());
}

namespace Infrastructure.Extensions;

using Azure.Security.KeyVault.Secrets;

public static class SecretClientExtensions
{
    extension(SecretClient secretClient)
    {
#pragma warning disable SA1009
        public (
            KeyVaultSecret ElasticsearchUsername,
            KeyVaultSecret ElasticsearchPassword,
            KeyVaultSecret ResendApiToken,
            KeyVaultSecret SqlServerUserId,
            KeyVaultSecret SqlServerPassword,
            KeyVaultSecret RedisPassword,
            KeyVaultSecret MongoDbUsername,
            KeyVaultSecret MongoDbPassword,
            KeyVaultSecret AdminEmail
        ) GetInfrastructureSecrets()
        {
            var elasticsearchUsername = secretClient.GetSecret("ElasticsearchUsername");
            var elasticsearchPassword = secretClient.GetSecret("ElasticsearchPassword");
            var resendApiToken = secretClient.GetSecret("ResendApiToken");
            var sqlServerUserId = secretClient.GetSecret("IdentitySqlServerUserId");
            var sqlServerPassword = secretClient.GetSecret("IdentitySqlServerPassword");
            var redisPassword = secretClient.GetSecret("RedisPassword");
            var mongoDbUsername = secretClient.GetSecret("MongoDbUsername");
            var mongoDbPassword = secretClient.GetSecret("MongoDbPassword");
            var adminEmail = secretClient.GetSecret("AdminEmail");
            return (
                elasticsearchUsername.Value,
                elasticsearchPassword.Value,
                resendApiToken.Value,
                sqlServerUserId.Value,
                sqlServerPassword.Value,
                redisPassword.Value,
                mongoDbUsername.Value,
                mongoDbPassword.Value,
                adminEmail.Value
            );
        }
#pragma warning restore SA1009
    }
}
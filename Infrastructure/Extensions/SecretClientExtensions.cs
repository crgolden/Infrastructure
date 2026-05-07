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
            KeyVaultSecret MonitoringRecipientEmail
        ) GetInfrastructureSecrets()
        {
            var elasticsearchUsername = secretClient.GetSecret("ElasticsearchUsername");
            var elasticsearchPassword = secretClient.GetSecret("ElasticsearchPassword");
            var resendApiToken = secretClient.GetSecret("ResendApiToken");
            var sqlServerUserId = secretClient.GetSecret("SqlServerUserId");
            var sqlServerPassword = secretClient.GetSecret("SqlServerPassword");
            var redisPassword = secretClient.GetSecret("RedisPassword");
            var mongoDbUsername = secretClient.GetSecret("MongoDbUsername");
            var mongoDbPassword = secretClient.GetSecret("MongoDbPassword");
            var monitoringRecipientEmail = secretClient.GetSecret("MonitoringRecipientEmail");
            return (
                elasticsearchUsername.Value,
                elasticsearchPassword.Value,
                resendApiToken.Value,
                sqlServerUserId.Value,
                sqlServerPassword.Value,
                redisPassword.Value,
                mongoDbUsername.Value,
                mongoDbPassword.Value,
                monitoringRecipientEmail.Value
            );
        }
#pragma warning restore SA1009
    }
}
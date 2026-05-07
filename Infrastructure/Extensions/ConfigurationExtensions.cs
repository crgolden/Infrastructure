namespace Infrastructure.Extensions;

public static class ConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        public T GetRequired<T>(string key)
            where T : notnull
        {
            return configuration.GetValue<T?>(key) ?? throw new InvalidOperationException($"Invalid '{key}'.");
        }

#pragma warning disable SA1009
        internal (
            string ElasticsearchUsername,
            string ElasticsearchPassword,
            string ResendApiToken,
            string SqlServerUserId,
            string SqlServerPassword,
            string RedisPassword,
            string MongoDbUsername,
            string MongoDbPassword,
            string MonitoringRecipientEmail
        ) GetInfrastructureSecrets()
        {
            var elasticsearchUsername = configuration.GetRequired<string>("ElasticsearchUsername");
            var elasticsearchPassword = configuration.GetRequired<string>("ElasticsearchPassword");
            var resendApiToken = configuration.GetRequired<string>("ResendApiToken");
            var sqlServerUserId = configuration.GetRequired<string>("SqlServerUserId");
            var sqlServerPassword = configuration.GetRequired<string>("SqlServerPassword");
            var redisPassword = configuration.GetRequired<string>("RedisPassword");
            var mongoDbUsername = configuration.GetRequired<string>("MongoDbUsername");
            var mongoDbPassword = configuration.GetRequired<string>("MongoDbPassword");
            var monitoringRecipientEmail = configuration.GetRequired<string>("MonitoringRecipientEmail");
            return (
                elasticsearchUsername,
                elasticsearchPassword,
                resendApiToken,
                sqlServerUserId,
                sqlServerPassword,
                redisPassword,
                mongoDbUsername,
                mongoDbPassword,
                monitoringRecipientEmail
            );
        }
#pragma warning restore SA1009
    }
}

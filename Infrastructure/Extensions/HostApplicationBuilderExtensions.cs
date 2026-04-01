namespace Infrastructure.Extensions;

using System.Net.Sockets;
using Azure.Core;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Infrastructure.HealthChecks;
using Infrastructure.Hubs;
using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Resend;
using Serilog;
using StackExchange.Redis;

public static class HostApplicationBuilderExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public async Task<IHostApplicationBuilder> AddMonitoringServicesAsync(SecretClient secretClient, CancellationToken cancellationToken = default)
        {
            // Fetch secrets
            var tasks = new[]
            {
                secretClient.GetSecretAsync("ResendApiKey", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("SqlServerUserId", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("SqlServerPassword", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("RedisPassword", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("MongoDbUsername", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("MongoDbPassword", cancellationToken: cancellationToken),
            };
            var results = await Task.WhenAll(tasks);
            var resendApiKey = results[0].Value.Value;
            var sqlUserId = results[1].Value.Value;
            var sqlPassword = results[2].Value.Value;
            var redisPassword = results[3].Value.Value;
            var mongoUsername = results[4].Value.Value;
            var mongoPassword = results[5].Value.Value;

            // Validate alert configuration
            var recipientEmail = builder.Configuration.GetValue<string>("AlertOptions:RecipientEmail");
            if (IsNullOrWhiteSpace(resendApiKey))
            {
                throw new InvalidOperationException("Key Vault secret 'ResendApiKey' is missing or empty.");
            }

            if (IsNullOrWhiteSpace(recipientEmail))
            {
                throw new InvalidOperationException("Configuration 'AlertOptions:RecipientEmail' is missing or empty.");
            }

            // Options
            builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("MonitoringOptions"));
            builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection("AlertOptions"));
            builder.Services.Configure<ServiceEndpointOptions>(builder.Configuration.GetSection("ServiceEndpoints"));

            // Resend
            builder.Services
                .Configure<ResendClientOptions>(configureOptions =>
                {
                    configureOptions.ApiToken = resendApiKey;
                })
                .AddHttpClient<ResendClient>().Services
                .AddTransient<IResend, ResendClient>();

            // Named HttpClient for health checks (ignores TLS cert errors for self-signed local certs)
            builder.Services.AddHttpClient("HealthCheck")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                });

            // SQL Server connection factory
            var sqlBuilder = builder.Configuration.GetSection("SqlConnectionStringBuilder").Get<SqlConnectionStringBuilder>()
                ?? throw new InvalidOperationException("Invalid 'SqlConnectionStringBuilder' configuration.");
            sqlBuilder.UserID = sqlUserId;
            sqlBuilder.Password = sqlPassword;
            var sqlConnectionString = sqlBuilder.ConnectionString;
            builder.Services.AddTransient<Func<SqlConnection>>(_ => () => new SqlConnection(sqlConnectionString));

            // Redis
            var redisHost = builder.Configuration.GetValue<string>("RedisHost") ?? throw new InvalidOperationException("Invalid 'RedisHost'.");
            var redisPort = builder.Configuration.GetValue<int?>("RedisPort") ?? throw new InvalidOperationException("Invalid 'RedisPort'.");
            var redisOptions = new ConfigurationOptions
            {
                Password = redisPassword,
                EndPoints = [new System.Net.DnsEndPoint(redisHost, redisPort)],
            };
            var muxer = await ConnectionMultiplexer.ConnectAsync(redisOptions);
            builder.Services.AddSingleton<IConnectionMultiplexer>(muxer);

            // MongoDB
            var mongoHost = builder.Configuration.GetValue<string>("MongoDbHost") ?? throw new InvalidOperationException("Invalid 'MongoDbHost'.");
            var mongoPort = builder.Configuration.GetValue<int?>("MongoDbPort") ?? throw new InvalidOperationException("Invalid 'MongoDbPort'.");
            var mongoSettings = new MongoClientSettings
            {
                Server = new MongoServerAddress(mongoHost, mongoPort),
                Credential = MongoCredential.CreateCredential("admin", mongoUsername, mongoPassword),
            };
            builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoSettings));

            // TcpClient factory for Yawcam
            builder.Services.AddTransient<Func<TcpClient>>(_ => () => new TcpClient());

            // Health checks
            builder.Services.AddHealthChecks()
                .AddCheck<IISHttpsHealthCheck>("IIS HTTPS", tags: ["iis"])
                .AddCheck<IISHttpHealthCheck>("IIS HTTP (CertifyTheWeb)", tags: ["iis"])
                .AddCheck<SqlServerHealthCheck>("SQL Server", tags: ["database"])
                .AddCheck<ElasticsearchHealthCheck>("Elasticsearch", tags: ["search"])
                .AddCheck<KibanaHealthCheck>("Kibana", tags: ["analytics"])
                .AddCheck<PlexHealthCheck>("Plex", tags: ["media"])
                .AddCheck<YawcamHealthCheck>("Yawcam AI", tags: ["surveillance"])
                .AddCheck<RedisHealthCheck>("Redis", tags: ["cache"])
                .AddCheck<MongoDbHealthCheck>("MongoDB", tags: ["database"]);

            // SignalR
            builder.Services.AddSignalR();

            // Alert service
            builder.Services.AddSingleton<IAlertService, AlertService>();

            // Health monitor (singleton state + background service)
            builder.Services.AddSingleton<IHealthMonitorService, HealthMonitorService>();
            builder.Services.AddHostedService(sp => (HealthMonitorService)sp.GetRequiredService<IHealthMonitorService>());

            return builder;
        }

        public async Task<IHostApplicationBuilder> AddObservabilityAsync(SecretClient secretClient, CancellationToken cancellationToken = default)
        {
            var elasticsearchNode = builder.Configuration.GetValue<Uri>("ElasticsearchNode") ?? throw new InvalidOperationException("Invalid 'ElasticsearchNode'.");
            var tasks = new[]
            {
                secretClient.GetSecretAsync("ElasticsearchUsername", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("ElasticsearchPassword", cancellationToken: cancellationToken),
            };
            var result = await Task.WhenAll(tasks);
            builder.Logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
            {
                openTelemetryLoggerOptions.IncludeFormattedMessage = true;
                openTelemetryLoggerOptions.IncludeScopes = true;
            });
            builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(x =>
                {
                    x.AddService(
                        serviceName: builder.Environment.ApplicationName,
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0");
                    x.AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant(),
                    });
                })
                .UseAzureMonitor()
                .WithMetrics(meterProviderBuilder =>
                {
                    meterProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();
                })
                .WithTracing(tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        .SetSampler(new AlwaysOnSampler())
                        .AddSource(builder.Environment.ApplicationName)
                        .AddAspNetCoreInstrumentation(aspNetCoreTraceInstrumentationOptions =>
                        {
                            aspNetCoreTraceInstrumentationOptions.Filter = context =>
                                !context.Request.Path.StartsWithSegments("/health");
                        })
                        .AddHttpClientInstrumentation();
                    if (builder.Environment.IsDevelopment())
                    {
                        tracerProviderBuilder.AddConsoleExporter();
                    }
                }).Services
                .AddSerilog((sp, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .ReadFrom.Configuration(builder.Configuration)
                        .ReadFrom.Services(sp)
                        .Enrich.FromLogContext();
                    if (builder.Environment.IsProduction())
                    {
                        loggerConfiguration
                            .WriteTo.Elasticsearch(
                                [elasticsearchNode],
                                elasticsearchSinkOptions =>
                                {
                                    elasticsearchSinkOptions.DataStream = new DataStreamName("logs", "dotnet", nameof(Infrastructure));
                                    elasticsearchSinkOptions.BootstrapMethod = BootstrapMethod.Failure;
                                },
                                transportConfiguration =>
                                {
                                    var header = new BasicAuthentication(result[0].Value.Value, result[1].Value.Value);
                                    transportConfiguration.Authentication(header);
                                });
                    }
                });
            return builder;
        }

        public IHostApplicationBuilder AddDataProtection(TokenCredential tokenCredential)
        {
            var blobUrl = builder.Configuration.GetValue<Uri>("BlobUri") ?? throw new InvalidOperationException("Invalid 'BlobUri'.");
            var dataProtectionKeyIdentifier = builder.Configuration.GetValue<Uri>("DataProtectionKeyIdentifier") ?? throw new InvalidOperationException("Invalid 'DataProtectionKeyIdentifier'.");
            builder.Services
                .AddDataProtection()
                .SetApplicationName(builder.Environment.ApplicationName)
                .PersistKeysToAzureBlobStorage(blobUrl, tokenCredential)
                .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
                .AddAzureClientsCore(true);
            return builder;
        }
    }
}

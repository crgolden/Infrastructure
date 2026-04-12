namespace Infrastructure.Extensions;

using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Azure.Core;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Models;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Resend;
using System.Text.Json.Serialization;
using Serilog;
using Services;
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
                secretClient.GetSecretAsync("ResendApiToken", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("SqlServerUserId", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("SqlServerPassword", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("RedisPassword", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("MongoDbUsername", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("MongoDbPassword", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("MonitoringRecipientEmail", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("ElasticsearchUsername", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("ElasticsearchPassword", cancellationToken: cancellationToken)
            };
            var results = await Task.WhenAll(tasks);
            var resendApiKey = results[0].Value.Value;
            if (IsNullOrWhiteSpace(resendApiKey))
            {
                throw new InvalidOperationException("Key Vault secret 'ResendApiToken' is missing or empty.");
            }

            var monitoringRecipientEmail = results[6].Value.Value;
            if (IsNullOrWhiteSpace(monitoringRecipientEmail))
            {
                throw new InvalidOperationException("Key Vault secret 'MonitoringRecipientEmail' is missing or empty.");
            }

            var sqlUserId = results[1].Value.Value;
            var sqlPassword = results[2].Value.Value;
            var redisPassword = results[3].Value.Value;
            var mongoUsername = results[4].Value.Value;
            var mongoPassword = results[5].Value.Value;
            var elasticsearchUsername = results[7].Value.Value;
            var elasticsearchPassword = results[8].Value.Value;

            // Options
            builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(nameof(MonitoringOptions)));
            builder.Services.Configure<AlertOptions>(alertOptions =>
            {
                alertOptions.RecipientEmail = monitoringRecipientEmail;
            });
            builder.Services.Configure<ServiceEndpointOptions>(builder.Configuration.GetSection(nameof(ServiceEndpointOptions)));

            // Resend
            builder.Services
                .Configure<ResendClientOptions>(resendClientOptions => resendClientOptions.ApiToken = resendApiKey)
                .AddHttpClient<ResendClient>().Services
                .AddTransient<IResend, ResendClient>();

            // Typed HttpClients for health checks
            builder.Services.AddHttpClient<IisHttpHealthCheck>();
            builder.Services.AddHttpClient<IisHttpsHealthCheck>();
            builder.Services.AddHttpClient<ElasticsearchHealthCheck>(httpClient =>
            {
                var credentials = System.Convert.ToBase64String(UTF8.GetBytes($"{elasticsearchUsername}:{elasticsearchPassword}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            });
            builder.Services.AddHttpClient<KibanaHealthCheck>(httpClient =>
            {
                var credentials = System.Convert.ToBase64String(UTF8.GetBytes($"{elasticsearchUsername}:{elasticsearchPassword}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            });
            builder.Services.AddHttpClient<PlexHealthCheck>();
            builder.Services.AddHttpClient<IdentityHealthCheck>();
            builder.Services.AddHttpClient<ManualsHealthCheck>();
            builder.Services.AddHttpClient<ExperienceHealthCheck>();

            // SQL Server connection factory
            builder.Services.AddTransient<Func<IDbConnection>>(sp => () =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var sqlBuilder = configuration.GetSection(nameof(SqlConnectionStringBuilder)).Get<SqlConnectionStringBuilder>() ?? throw new InvalidOperationException($"Invalid '{nameof(SqlConnectionStringBuilder)}' configuration.");
                if (sqlBuilder.IntegratedSecurity)
                {
                    return new SqlConnection(sqlBuilder.ConnectionString);
                }

                sqlBuilder.UserID = sqlUserId;
                sqlBuilder.Password = sqlPassword;
                return new SqlConnection(sqlBuilder.ConnectionString);
            });

            // Redis
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var redisHost = configuration.GetValue<string?>("RedisHost") ?? throw new InvalidOperationException("Invalid 'RedisHost'.");
                var redisPort = configuration.GetValue<int?>("RedisPort") ?? throw new InvalidOperationException("Invalid 'RedisPort'.");
                var endPoint = new DnsEndPoint(redisHost, redisPort);
                var redisOptions = new ConfigurationOptions
                {
                    Password = redisPassword,
                    EndPoints = [endPoint],
                    Ssl = true
                };
                return ConnectionMultiplexer.Connect(redisOptions);
            });

            // MongoDB
            builder.Services.AddSingleton<IMongoClient>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var mongoHost = configuration.GetValue<string?>("MongoDbHost") ?? throw new InvalidOperationException("Invalid 'MongoDbHost'.");
                var mongoPort = configuration.GetValue<int?>("MongoDbPort") ?? throw new InvalidOperationException("Invalid 'MongoDbPort'.");
                var mongoSettings = new MongoClientSettings
                {
                    Server = new MongoServerAddress(mongoHost, mongoPort),
                    Credential = MongoCredential.CreateCredential("admin", mongoUsername, mongoPassword),
                    UseTls = true
                };
                return new MongoClient(mongoSettings);
            });

            // TcpClient factory for Yawcam
            builder.Services.AddTransient<Func<TcpClient>>(_ => () => new TcpClient());

            // Health checks
            builder.Services.AddHealthChecks()
                .AddCheck<IisHttpsHealthCheck>("IIS HTTPS", tags: ["iis"])
                .AddCheck<IisHttpHealthCheck>("IIS HTTP (CertifyTheWeb)", tags: ["iis"])
                .AddCheck<SqlServerHealthCheck>("SQL Server", tags: ["database"])
                .AddCheck<ElasticsearchHealthCheck>("Elasticsearch", tags: ["search"])
                .AddCheck<KibanaHealthCheck>("Kibana", tags: ["analytics"])
                .AddCheck<PlexHealthCheck>("Plex", tags: ["media"])
                .AddCheck<YawcamHealthCheck>("Yawcam AI", tags: ["surveillance"])
                .AddCheck<RedisHealthCheck>("Redis", tags: ["cache"])
                .AddCheck<MongoDbHealthCheck>("MongoDB", tags: ["database"])
                .AddCheck<IdentityHealthCheck>("Identity", tags: ["service"])
                .AddCheck<ManualsHealthCheck>("Manuals", tags: ["service"])
                .AddCheck<ExperienceHealthCheck>("Experience", tags: ["service"]);

            // SignalR
            builder.Services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            // Alert service
            builder.Services.AddSingleton<IAlertService, AlertService>();

            // Health monitor (singleton state + background service)
            builder.Services.AddSingleton<IHealthMonitorService, HealthMonitorService>();
            builder.Services.AddHostedService(sp => (HealthMonitorService)sp.GetRequiredService<IHealthMonitorService>());

            // Keepalive: self-ping to prevent Azure Free tier idle recycling
            builder.Services.AddHttpClient<KeepaliveService>();
            builder.Services.AddHostedService<KeepaliveService>();

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
                    var serviceName = builder.Configuration["WEBSITE_SITE_NAME"] ?? builder.Environment.ApplicationName;
                    x.AddService(
                        serviceName: serviceName,
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
                        .AddMeter(nameof(Infrastructure))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();
                })
                .WithTracing(tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        .SetSampler(new AlwaysOnSampler())
                        .AddSource(nameof(Infrastructure))
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
                        .Enrich.FromLogContext()
                        .Enrich.WithMachineName()
                        .Enrich.WithEnvironmentName()
                        .Enrich.WithProperty("ApplicationName", "crgolden-infrastructure");
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

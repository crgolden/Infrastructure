#pragma warning disable SA1200
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Infrastructure.Extensions;
using Infrastructure.HealthChecks;
using Infrastructure.Hubs;
using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using MongoDB.Driver;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Resend;
using Serilog;
using StackExchange.Redis;
#pragma warning restore SA1200

Serilog.Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    string elasticsearchUsername, elasticsearchPassword, resendApiToken, monitoringRecipientEmail;
    IConfigurationSection monitoringOptionsSection = builder.Configuration.GetRequiredSection(nameof(MonitoringOptions)),
        serviceEndpointOptionsSection = builder.Configuration.GetRequiredSection(nameof(ServiceEndpointOptions)),
        sqlConnectionStringBuilderSection = builder.Configuration.GetRequiredSection(nameof(SqlConnectionStringBuilder));
    var sqlConnectionStringBuilder = sqlConnectionStringBuilderSection.Get<SqlConnectionStringBuilder>() ?? throw new InvalidOperationException($"Invalid '{nameof(SqlConnectionStringBuilder)}' configuration.");
    var redisHost = builder.Configuration.GetRequired<string>("RedisHost");
    var redisPort = builder.Configuration.GetRequired<int>("RedisPort");
    var redisSsl = builder.Configuration.GetRequired<bool>("RedisSsl");
    var redisEndpoint = new DnsEndPoint(redisHost, redisPort);
    var configurationOptions = new ConfigurationOptions
    {
        Ssl = redisSsl,
        EndPoints = [redisEndpoint]
    };
    string mongoDatabaseName = builder.Configuration.GetRequired<string>("MongoDatabaseName"),
        mongoServerHost = builder.Configuration.GetRequired<string>("MongoServerHost");
    var mongoServerPort = builder.Configuration.GetRequired<int>("MongoServerPort");
    var mongoUseTls = builder.Configuration.GetRequired<bool>("MongoUseTls");
    var mongoSettings = new MongoClientSettings
    {
        Server = new MongoServerAddress(mongoServerHost, mongoServerPort),
        UseTls = mongoUseTls
    };
    if (builder.Environment.IsProduction())
    {
        var defaultAzureCredentialOptionsSection = builder.Configuration.GetRequiredSection(nameof(DefaultAzureCredentialOptions));
        var defaultAzureCredentialOptions = defaultAzureCredentialOptionsSection.Get<DefaultAzureCredentialOptions>() ?? throw new InvalidOperationException($"Invalid '{nameof(DefaultAzureCredentialOptions)}' section.");
        var tokenCredential = new DefaultAzureCredential(defaultAzureCredentialOptions);
        Uri blobUri = builder.Configuration.GetRequired<Uri>("BlobUri"),
            dataProtectionKeyIdentifier = builder.Configuration.GetRequired<Uri>("DataProtectionKeyIdentifier"),
            elasticsearchNode = builder.Configuration.GetRequired<Uri>("ElasticsearchNode"),
            keyVaultUrl = builder.Configuration.GetRequired<Uri>("KeyVaultUri");
        var applicationName = builder.Configuration.GetRequired<string>("WEBSITE_SITE_NAME");
        var secretClient = new SecretClient(keyVaultUrl, tokenCredential);
        var secrets = secretClient.GetInfrastructureSecrets();
        elasticsearchUsername = secrets.ElasticsearchUsername.Value;
        elasticsearchPassword = secrets.ElasticsearchPassword.Value;
        resendApiToken = secrets.ResendApiToken.Value;
        monitoringRecipientEmail = secrets.MonitoringRecipientEmail.Value;
        sqlConnectionStringBuilder.UserID = secrets.SqlServerUserId.Value;
        sqlConnectionStringBuilder.Password = secrets.SqlServerPassword.Value;
        configurationOptions.Password = secrets.RedisPassword.Value;
        mongoSettings.Credential = MongoCredential.CreateCredential(mongoDatabaseName, secrets.MongoDbUsername.Value, secrets.MongoDbPassword.Value);
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase));
        builder.Logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
        {
            openTelemetryLoggerOptions.IncludeFormattedMessage = true;
            openTelemetryLoggerOptions.IncludeScopes = true;
        });
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider)
                .Enrich.WithProperty(nameof(IHostEnvironment.ApplicationName), applicationName)
                .WriteTo.Elasticsearch(
                    [elasticsearchNode],
                    elasticsearchSinkOptions =>
                    {
                        elasticsearchSinkOptions.DataStream = new DataStreamName("logs", "dotnet", nameof(Infrastructure));
                        elasticsearchSinkOptions.BootstrapMethod = BootstrapMethod.Failure;
                    },
                    transportConfiguration =>
                    {
                        var header = new BasicAuthentication(secrets.ElasticsearchUsername.Value, secrets.ElasticsearchPassword.Value);
                        transportConfiguration.Authentication(header);
                    }))
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder
                .AddService(applicationName, null, typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
                }))
            .WithMetrics(meterProviderBuilder => meterProviderBuilder
                .AddMeter(nameof(Infrastructure))
                .AddRuntimeInstrumentation())
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .SetSampler(new AlwaysOnSampler())
                .AddSource(nameof(Infrastructure)))
            .UseAzureMonitor().Services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToAzureBlobStorage(blobUri, tokenCredential)
            .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
            .AddAzureClientsCore(true);
    }
    else
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets("aspnet-Infrastructure-3f7a2c1b-8e4d-4b9f-a6c3-2d1e5f8b9a0c");
        }

        var secrets = builder.Configuration.GetInfrastructureSecrets();
        elasticsearchUsername = secrets.ElasticsearchUsername;
        elasticsearchPassword = secrets.ElasticsearchPassword;
        resendApiToken = secrets.ResendApiToken;
        monitoringRecipientEmail = secrets.MonitoringRecipientEmail;
        sqlConnectionStringBuilder.UserID = secrets.SqlServerUserId;
        sqlConnectionStringBuilder.Password = secrets.SqlServerPassword;
        configurationOptions.Password = secrets.RedisPassword;
        mongoSettings.Credential = MongoCredential.CreateCredential(mongoDatabaseName, secrets.MongoDbUsername, secrets.MongoDbPassword);
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider))
            .AddDataProtection()
            .UseEphemeralDataProtectionProvider();
    }

    builder.Services
        .Configure<MonitoringOptions>(monitoringOptionsSection)
        .Configure<AlertOptions>(alertOptions => alertOptions.RecipientEmail = monitoringRecipientEmail)
        .Configure<ServiceEndpointOptions>(serviceEndpointOptionsSection)
        .Configure<ResendClientOptions>(resendClientOptions => resendClientOptions.ApiToken = resendApiToken)
        .AddHttpClient<ResendClient>().Services
        .AddTransient<IResend, ResendClient>()
        .AddHttpClient<IisHttpHealthCheck>().Services
        .AddHttpClient<IisHttpsHealthCheck>().Services
        .AddHttpClient<ElasticsearchHealthCheck>(httpClient =>
        {
            var inArray = UTF8.GetBytes($"{elasticsearchUsername}:{elasticsearchPassword}");
            var credentials = System.Convert.ToBase64String(inArray);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }).Services
        .AddHttpClient<KibanaHealthCheck>(httpClient =>
        {
            var credentials = System.Convert.ToBase64String(UTF8.GetBytes($"{elasticsearchUsername}:{elasticsearchPassword}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }).Services
        .AddHttpClient<PlexHealthCheck>().Services
        .AddHttpClient<IdentityHealthCheck>().Services
        .AddHttpClient<ManualsHealthCheck>().Services
        .AddHttpClient<ExperienceHealthCheck>().Services
        .AddHttpClient<ProductsHealthCheck>().Services
        .AddTransient<Func<IDbConnection>>(_ => () => new SqlConnection(sqlConnectionStringBuilder.ConnectionString))
        .AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configurationOptions))
        .AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings))
        .AddTransient<Func<TcpClient>>(_ => () => new TcpClient())
        .AddHealthChecks()
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
        .AddCheck<ExperienceHealthCheck>("Experience", tags: ["service"])
        .AddCheck<ProductsHealthCheck>("Products", tags: ["service"]).Services
        .AddSignalR()
        .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Services
        .AddSingleton<IAlertService, AlertService>()
        .AddSingleton<HealthMonitorService>()
        .AddSingleton<IHealthMonitorService>(sp => sp.GetRequiredService<HealthMonitorService>())
        .AddHostedService(sp => sp.GetRequiredService<HealthMonitorService>())
        .AddHttpClient<KeepaliveService>().Services
        .AddHostedService<KeepaliveService>()
        .AddControllers().Services
        .AddRazorPages();

    var app = builder.Build();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, _) =>
        {
            if (Activity.Current is null)
            {
                return;
            }

            diagnosticContext.Set(nameof(Activity.TraceId), Activity.Current.TraceId.ToString());
            diagnosticContext.Set(nameof(Activity.SpanId), Activity.Current.SpanId.ToString());
        };
    });
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.MapHealthChecks("/health").DisableHttpMetrics();
    app.MapGet("/ping", () => Results.Ok()).DisableHttpMetrics();
    app.MapStaticAssets();
    app.MapControllers();
    app.MapRazorPages();
    app.MapHub<HealthHub>("/hubs/health").DisableHttpMetrics();
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Serilog.Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Serilog.Log.CloseAndFlushAsync();
}

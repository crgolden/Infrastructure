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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;
#pragma warning restore SA1200

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    string elasticsearchUsername, elasticsearchPassword, adminEmail, infrastructureClientId, infrastructureClientSecret;
    var oidcAuthority = builder.Configuration.GetRequired<Uri>("OidcAuthority");
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
        EndPoints = [redisEndpoint],
        AbortOnConnectFail = false
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
        string applicationName = builder.Configuration.GetRequired<string>("WEBSITE_SITE_NAME"),
            serviceBusNamespace = builder.Configuration.GetRequired<string>("ServiceBusNamespace");
        var secretClient = new SecretClient(keyVaultUrl, tokenCredential);
        var secrets = secretClient.GetInfrastructureSecrets();
        elasticsearchUsername = secrets.ElasticsearchUsername.Value;
        elasticsearchPassword = secrets.ElasticsearchPassword.Value;
        adminEmail = secrets.AdminEmail.Value;
        infrastructureClientId = secrets.InfrastructureClientId.Value;
        infrastructureClientSecret = secrets.InfrastructureClientSecret.Value;
        sqlConnectionStringBuilder.UserID = secrets.SqlServerUserId.Value;
        sqlConnectionStringBuilder.Password = secrets.SqlServerPassword.Value;
        configurationOptions.Password = secrets.RedisPassword.Value;
        mongoSettings.Credential = MongoCredential.CreateCredential(mongoDatabaseName, secrets.MongoDbUsername.Value, secrets.MongoDbPassword.Value);
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) && !context.Request.Path.StartsWithSegments("/ping", StringComparison.OrdinalIgnoreCase));
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
                        elasticsearchSinkOptions.TextFormatting.MapCustom = (ecsDocument, _) =>
                        {
                            ecsDocument.Service ??= new Elastic.CommonSchema.Service();
                            ecsDocument.Service.Name = applicationName;
                            return ecsDocument;
                        };
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
                .AddRuntimeInstrumentation()
                .AddView(instrument =>
                    instrument.Meter.Name == "System.Net.Http" ? MetricStreamConfiguration.Drop : null)
                .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration.GetRequired<string>("AlloyEndpoint"))))
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .SetSampler(new AlwaysOnSampler())
                .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration.GetRequired<string>("AlloyEndpoint"))))
            .UseAzureMonitor().Services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToAzureBlobStorage(blobUri, tokenCredential)
            .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
            .AddAzureClients(azureClientFactoryBuilder =>
            {
                azureClientFactoryBuilder.UseCredential(tokenCredential);
                azureClientFactoryBuilder.AddServiceBusClientWithNamespace(serviceBusNamespace).WithName("crgolden");
            });
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
        adminEmail = secrets.AdminEmail;
        infrastructureClientId = secrets.InfrastructureClientId;
        infrastructureClientSecret = secrets.InfrastructureClientSecret;
        sqlConnectionStringBuilder.UserID = secrets.SqlServerUserId;
        sqlConnectionStringBuilder.Password = secrets.SqlServerPassword;
        configurationOptions.Password = secrets.RedisPassword;
        mongoSettings.Credential = MongoCredential.CreateCredential(mongoDatabaseName, secrets.MongoDbUsername, secrets.MongoDbPassword);
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider))
            .AddDataProtection()
            .UseEphemeralDataProtectionProvider().Services
            .AddAzureClients(azureClientFactoryBuilder =>
            {
                azureClientFactoryBuilder.AddServiceBusClient(secrets.ServiceBusConnectionString).WithName("crgolden");
            });
    }

    builder.Services
        .Configure<MonitoringOptions>(monitoringOptionsSection)
        .Configure<AlertOptions>(alertOptions => alertOptions.RecipientEmail = adminEmail)
        .Configure<ServiceEndpointOptions>(serviceEndpointOptionsSection)
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
        .AddHttpClient<HomeAssistantHealthCheck>().Services
        .AddHttpClient<UptimeKumaHealthCheck>().Services
        .AddHttpClient<GrafanaHealthCheck>().Services
        .AddHttpClient<IdentityHealthCheck>().Services
        .AddHttpClient<ManualsHealthCheck>().Services
        .AddHttpClient<InventoryHealthCheck>().Services
        .AddHttpClient<ProductsHealthCheck>().Services
        .AddHttpClient<ChurchesHealthCheck>().Services
        .AddHttpClient<DirectoryHealthCheck>().Services
        .AddTransient<Func<IDbConnection>>(_ => () => new SqlConnection(sqlConnectionStringBuilder.ConnectionString))
        .AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configurationOptions))
        .AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings))
        .AddTransient<Func<TcpClient>>(_ => () => new TcpClient())
        .AddHealthChecks()
        .AddCheck<IisHttpsHealthCheck>("IIS HTTPS", tags: ["iis"])
        .AddCheck<SqlServerHealthCheck>("SQL Server", tags: ["database"])
        .AddCheck<ElasticsearchHealthCheck>("Elasticsearch", tags: ["search"])
        .AddCheck<KibanaHealthCheck>("Kibana", tags: ["analytics"])
        .AddCheck<PlexHealthCheck>("Plex", tags: ["media"])
        .AddCheck<HomeAssistantHealthCheck>("Home Assistant", tags: ["home"])
        .AddCheck<UptimeKumaHealthCheck>("Uptime Kuma", tags: ["monitoring"])
        .AddCheck<GrafanaHealthCheck>("Grafana", tags: ["monitoring"])
        .AddCheck<AlloyHealthCheck>("Alloy", tags: ["monitoring"])
        .AddCheck<YawcamHealthCheck>("Yawcam AI", tags: ["surveillance"])
        .AddCheck<WMSvcHealthCheck>("WMSvc", tags: ["service"])
        .AddCheck<RedisHealthCheck>("Redis", tags: ["cache"])
        .AddCheck<MongoDbHealthCheck>("MongoDB", tags: ["database"])
        .AddCheck<IdentityHealthCheck>("Identity", tags: ["service"])
        .AddCheck<ManualsHealthCheck>("Manuals", tags: ["service"])
        .AddCheck<InventoryHealthCheck>("Inventory", tags: ["service"])
        .AddCheck<ProductsHealthCheck>("Products", tags: ["service"])
        .AddCheck<ChurchesHealthCheck>("Churches", tags: ["service"])
        .AddCheck<DirectoryHealthCheck>("Directory", tags: ["service"]).Services
        .AddSignalR()
        .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Services
        .AddSingleton<IAlertService, AlertService>()
        .AddSingleton<HealthMonitorService>()
        .AddSingleton<IHealthMonitorService>(sp => sp.GetRequiredService<HealthMonitorService>())
        .AddHostedService(sp => sp.GetRequiredService<HealthMonitorService>())
        .AddHttpClient<KeepaliveService>().Services
        .AddHostedService<KeepaliveService>()
        .AddControllers().Services
        .AddRazorPages()
        .Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.Authority = oidcAuthority.ToString();
            options.ClientId = infrastructureClientId;
            options.ClientSecret = infrastructureClientSecret;
            options.ResponseType = "code";
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.MapInboundClaims = false;
            if (!builder.Environment.IsProduction())
            {
                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        var server = context.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
                        var addresses = server.Features.GetRequiredFeature<IServerAddressesFeature>().Addresses;
                        var address = addresses.FirstOrDefault(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            ?? addresses.FirstOrDefault();
                        if (!IsNullOrWhiteSpace(address))
                        {
                            context.ProtocolMessage.RedirectUri = address.TrimEnd('/') + options.CallbackPath;
                        }

                        return Task.CompletedTask;
                    },
                    OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        var server = context.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
                        var addresses = server.Features.GetRequiredFeature<IServerAddressesFeature>().Addresses;
                        var address = addresses.FirstOrDefault(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            ?? addresses.FirstOrDefault();
                        if (!IsNullOrWhiteSpace(address))
                        {
                            context.ProtocolMessage.PostLogoutRedirectUri = address.TrimEnd('/') + options.SignedOutCallbackPath;
                        }

                        return Task.CompletedTask;
                    }
                };
            }
        }).Services
        .AddAuthorization();

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
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status200OK,
        }
    }).DisableHttpMetrics();
    app.MapGet("/ping", () => Results.Ok()).DisableHttpMetrics();
    app.MapStaticAssets();
    app.MapControllers();
    app.MapRazorPages();
    app.MapHub<HealthHub>("/hubs/health").DisableHttpMetrics();
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

namespace Infrastructure.Services;

using System.Diagnostics;
using OpenTelemetry;

public sealed class KeepaliveService : BackgroundService
{
    internal const string HostnameConfigurationKey = "WEBSITE_HOSTNAME";

    private readonly TimeSpan _interval;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly Uri? _pingUri;

    public KeepaliveService(HttpClient httpClient, IConfiguration configuration)
        : this(httpClient, configuration, TimeSpan.FromMinutes(10), TimeProvider.System)
    {
    }

    internal KeepaliveService(
        HttpClient httpClient,
        IConfiguration configuration,
        TimeSpan interval,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _interval = interval;
        _timeProvider = timeProvider ?? TimeProvider.System;
        var hostname = configuration[HostnameConfigurationKey];
        _pingUri = IsNullOrWhiteSpace(hostname) ? null : PingUri(hostname);
    }

    internal static Uri PingUri(string hostname) => new Uri($"https://{hostname}/ping");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_pingUri is null)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, _timeProvider, stoppingToken);
            try
            {
                using (SuppressInstrumentationScope.Begin())
                {
                    await _httpClient.GetAsync(_pingUri, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Activity.Current?.AddException(ex);
            }
        }
    }
}

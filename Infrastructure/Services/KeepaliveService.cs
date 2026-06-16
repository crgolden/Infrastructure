namespace Infrastructure.Services;

using System.Diagnostics;
using OpenTelemetry;

public sealed class KeepaliveService : BackgroundService
{
    private readonly TimeSpan _interval;
    private readonly HttpClient _httpClient;
    private readonly Uri? _pingUri;

    public KeepaliveService(HttpClient httpClient, IConfiguration configuration)
        : this(httpClient, configuration, TimeSpan.FromMinutes(10))
    {
    }

    internal KeepaliveService(HttpClient httpClient, IConfiguration configuration, TimeSpan interval)
    {
        _httpClient = httpClient;
        _interval = interval;
        var hostname = configuration["WEBSITE_HOSTNAME"];
        _pingUri = IsNullOrEmpty(hostname) ? null : new Uri($"https://{hostname}/ping");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_pingUri is null)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, stoppingToken);
            try
            {
                // Suppress the keepalive self-ping dependency span — internal warmup noise, not actionable.
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

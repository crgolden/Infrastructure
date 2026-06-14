namespace Infrastructure.Services;

using System.Diagnostics;
using OpenTelemetry;

public sealed class KeepaliveService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly HttpClient _httpClient;
    private readonly Uri? _pingUri;

    public KeepaliveService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
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
            await Task.Delay(Interval, stoppingToken);
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

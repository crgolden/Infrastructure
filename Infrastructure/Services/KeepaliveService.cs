namespace Infrastructure.Services;

public sealed partial class KeepaliveService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly HttpClient _httpClient;
    private readonly Uri? _pingUri;
    private readonly ILogger<KeepaliveService> _logger;

    public KeepaliveService(HttpClient httpClient, IConfiguration configuration, ILogger<KeepaliveService> logger)
    {
        _httpClient = httpClient;
        var hostname = configuration["WEBSITE_HOSTNAME"];
        _pingUri = IsNullOrEmpty(hostname) ? null : new Uri($"https://{hostname}/ping");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_pingUri is null)
        {
            return;
        }

        LogKeepaliveStarted(_logger, _pingUri);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            try
            {
                await _httpClient.GetAsync(_pingUri, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogKeepaliveFailed(_logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Keepalive started. Pinging {Uri} every 10 minutes.")]
    private static partial void LogKeepaliveStarted(ILogger logger, Uri uri);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Keepalive ping failed.")]
    private static partial void LogKeepaliveFailed(ILogger logger, Exception ex);
}

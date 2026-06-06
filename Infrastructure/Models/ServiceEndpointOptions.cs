namespace Infrastructure.Models;

public sealed class ServiceEndpointOptions
{
    public Uri? IisHttps { get; set; }

    public Uri? Elasticsearch { get; set; }

    public Uri? Kibana { get; set; }

    public Uri? Plex { get; set; }

    public Uri? HomeAssistant { get; set; }

    public Uri? UptimeKuma { get; set; }

    public string? YawcamHost { get; set; }

    public int? YawcamPort { get; set; }

    public string? WmsvcHost { get; set; }

    public int? WmsvcPort { get; set; }
}

namespace Infrastructure.Models;

public sealed class ServiceEndpointOptions
{
    public Uri? IisHttp { get; set; }

    public Uri? IisHttps { get; set; }

    public Uri? Elasticsearch { get; set; }

    public Uri? Kibana { get; set; }

    public Uri? Plex { get; set; }

    public string? YawcamHost { get; set; }

    public int? YawcamPort { get; set; }
}

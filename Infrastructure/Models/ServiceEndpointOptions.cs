namespace Infrastructure.Models;

public sealed class ServiceEndpointOptions
{
    public Uri IisHttp { get; set; } = new("http://localhost/");
    public Uri IisHttps { get; set; } = new("https://localhost/");
    public Uri Elasticsearch { get; set; } = new("http://localhost:9200/_cluster/health");
    public Uri Kibana { get; set; } = new("http://localhost:5601/api/status");
    public Uri Plex { get; set; } = new("http://localhost:32400/identity");
    public string YawcamHost { get; set; } = "localhost";
    public int YawcamPort { get; set; } = 5995;
}

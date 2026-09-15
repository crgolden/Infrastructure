namespace Infrastructure.Tests.Unit.TestSupport;

using System.Net;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;
    private readonly Exception? _throwOnSend;
    private readonly List<Uri?> _requestedUris = [];

    private StubHttpMessageHandler(HttpStatusCode statusCode, string content, Exception? throwOnSend)
    {
        _statusCode = statusCode;
        _content = content;
        _throwOnSend = throwOnSend;
    }

    internal IReadOnlyList<Uri?> RequestedUris => _requestedUris;

    internal static HttpClient RespondingWith(HttpStatusCode statusCode, string content) =>
        new HttpClient(new StubHttpMessageHandler(statusCode, content, throwOnSend: null));

    internal static HttpClient Throwing(Exception toThrow) =>
        new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, string.Empty, toThrow));

    internal static StubHttpMessageHandler Recording() =>
        new StubHttpMessageHandler(HttpStatusCode.OK, string.Empty, throwOnSend: null);

    internal static StubHttpMessageHandler RecordingAndThrowing(Exception toThrow) =>
        new StubHttpMessageHandler(HttpStatusCode.OK, string.Empty, toThrow);

    internal HttpClient ToClient() => new HttpClient(this);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requestedUris.Add(request.RequestUri);

        if (_throwOnSend is not null)
        {
            return Task.FromException<HttpResponseMessage>(_throwOnSend);
        }

        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content),
        });
    }
}

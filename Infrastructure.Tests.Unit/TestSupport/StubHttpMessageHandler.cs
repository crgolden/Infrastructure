namespace Infrastructure.Tests.Unit.TestSupport;

using System.Net;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;
    private readonly Exception? _throwOnSend;

    private StubHttpMessageHandler(HttpStatusCode statusCode, string content, Exception? throwOnSend)
    {
        _statusCode = statusCode;
        _content = content;
        _throwOnSend = throwOnSend;
    }

    internal static HttpClient RespondingWith(HttpStatusCode statusCode, string content) =>
        new HttpClient(new StubHttpMessageHandler(statusCode, content, throwOnSend: null));

    internal static HttpClient Throwing(Exception toThrow) =>
        new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, string.Empty, toThrow));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
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

using System.Net;

namespace Template.UnitTests.TestHelpers;

/// <summary>
/// Test double for HttpMessageHandler to intercept HTTP requests in tests.
/// </summary>
public sealed class HttpMessageHandlerStub : HttpMessageHandler
{
    public HttpResponseMessage? ResponseMessage { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan DelayResponse { get; set; } = TimeSpan.Zero;
    public HttpRequestMessage? LastRequest { get; private set; }
    public List<HttpRequestMessage> AllRequests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        AllRequests.Add(request);

        if (DelayResponse > TimeSpan.Zero)
        {
            await Task.Delay(DelayResponse, cancellationToken);
        }

        return Exception is not null
            ? throw Exception
            : ResponseMessage ?? new HttpResponseMessage(HttpStatusCode.OK);
    }
}

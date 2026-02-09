using Microsoft.Extensions.Options;

namespace Template.Api.Infrastructure.Nexus;

/// <summary>
/// Delegating handler that attaches the Nexus API key to outgoing requests.
/// Reads the key from NexusClientOptions (configured via environment variable or appsettings).
/// </summary>
public sealed class NexusApiKeyHandler : DelegatingHandler
{
    private readonly NexusClientOptions _options;

    public NexusApiKeyHandler(IOptions<NexusClientOptions> options)
    {
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

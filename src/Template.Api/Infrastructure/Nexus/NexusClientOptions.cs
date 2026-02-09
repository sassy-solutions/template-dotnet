namespace Template.Api.Infrastructure.Nexus;

/// <summary>
/// Configuration options for the Nexus client.
/// Bound from the "Nexus" configuration section.
/// </summary>
/// <remarks>
/// In production, ApiKey is injected via K8s secret as NEXUS__APIKEY environment variable.
/// </remarks>
public sealed class NexusClientOptions
{
    public const string SectionName = "Nexus";

    /// <summary>Base URL of the Nexus API.</summary>
    public string BaseUrl { get; set; } = "http://nexus-api.nexus-live.svc.cluster.local";

    /// <summary>API key for authenticating with Nexus (nxs_ prefix).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Timeout for HTTP requests to Nexus in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}

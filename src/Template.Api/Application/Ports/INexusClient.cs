using Template.Api.Infrastructure.Nexus.Dto;

namespace Template.Api.Application.Ports;

/// <summary>
/// Port for communicating with the Nexus platform tenant API.
/// Implementations handle HTTP transport, authentication, and serialization.
/// </summary>
/// <remarks>
/// Usage patterns:
/// <list type="bullet">
/// <item>Declarative: Use [NexusTrack], [NexusAuthorize], [NexusFeature] attributes on endpoints</item>
/// <item>Direct: Inject INexusClient for orchestration scenarios requiring fine-grained control</item>
/// </list>
/// </remarks>
public interface INexusClient
{
    /// <summary>Tracks a usage event in Nexus.</summary>
    Task<NexusResult<TrackResponse>> TrackAsync(
        string eventName,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets all usage counters for the authenticated organization.</summary>
    Task<NexusResult<UsageResponse>> GetUsageAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Gets the usage counter for a specific event.</summary>
    Task<NexusResult<TrackResponse>> GetUsageByEventAsync(
        string eventName,
        CancellationToken cancellationToken = default);

    /// <summary>Checks if the Nexus API is healthy (no auth required).</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    // Future: CheckFeatureAsync, ValidateAuthorizationAsync, GetBillingStatusAsync
}

/// <summary>
/// Lightweight Result wrapper for Nexus API calls.
/// Avoids exceptions for expected failures (HTTP 4xx/5xx).
/// </summary>
public sealed record NexusResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }

    public static NexusResult<T> Success(T value) =>
        new() { IsSuccess = true, Value = value, StatusCode = 200 };

    public static NexusResult<T> Failure(string error, int statusCode) =>
        new() { IsSuccess = false, Error = error, StatusCode = statusCode };
}

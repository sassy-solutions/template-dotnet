using Template.Api.Application.Ports;
using Template.Api.Infrastructure.Nexus.Dto;

namespace Template.Api.Infrastructure.Nexus;

/// <summary>
/// Typed HttpClient adapter for the Nexus tenant API.
/// Implements the INexusClient port.
/// </summary>
public sealed class NexusClient : INexusClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NexusClient> _logger;

    public NexusClient(HttpClient httpClient, ILogger<NexusClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<NexusResult<TrackResponse>> TrackAsync(
        string eventName,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new TrackRequest(eventName, metadata);
            var response = await _httpClient.PostAsJsonAsync("api/v1/track", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Nexus track failed: {StatusCode} {Error}", (int)response.StatusCode, error);
                return NexusResult<TrackResponse>.Failure(error, (int)response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<TrackResponse>(cancellationToken);
            return NexusResult<TrackResponse>.Success(result!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to track event {Event} in Nexus", eventName);
            return NexusResult<TrackResponse>.Failure(ex.Message, 0);
        }
    }

    public async Task<NexusResult<UsageResponse>> GetUsageAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/usage", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return NexusResult<UsageResponse>.Failure(error, (int)response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<UsageResponse>(cancellationToken);
            return NexusResult<UsageResponse>.Success(result!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get usage from Nexus");
            return NexusResult<UsageResponse>.Failure(ex.Message, 0);
        }
    }

    public async Task<NexusResult<TrackResponse>> GetUsageByEventAsync(
        string eventName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/v1/usage/{Uri.EscapeDataString(eventName)}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return NexusResult<TrackResponse>.Failure(error, (int)response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<TrackResponse>(cancellationToken);
            return NexusResult<TrackResponse>.Success(result!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get usage for event {Event}", eventName);
            return NexusResult<TrackResponse>.Failure(ex.Message, 0);
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Template.Api.Infrastructure.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Nexus platform.
/// </summary>
/// <remarks>
/// This check is tagged as "ready" to be included in the readiness probe.
/// It attempts to call Nexus's health endpoint to ensure the platform is reachable.
/// </remarks>
public class NexusHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<NexusHealthCheck> _logger;

    public NexusHealthCheck(IHttpClientFactory clientFactory, ILogger<NexusHealthCheck> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _clientFactory.CreateClient("Nexus");

            // Set a short timeout to avoid blocking the health check
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            var response = await client.GetAsync("/health", cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Nexus health check succeeded");
                return HealthCheckResult.Healthy("Nexus is reachable");
            }

            _logger.LogWarning("Nexus returned non-success status code: {StatusCode}", response.StatusCode);
            return HealthCheckResult.Unhealthy($"Nexus returned status code {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Nexus health check was cancelled");
            return HealthCheckResult.Unhealthy("Nexus health check was cancelled");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Nexus health check timed out");
            return HealthCheckResult.Unhealthy("Nexus health check timed out after 2 seconds");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Nexus health check failed due to HTTP request error");
            return HealthCheckResult.Unhealthy("Nexus is unreachable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nexus health check failed with unexpected error");
            return HealthCheckResult.Unhealthy("Nexus health check failed", ex);
        }
    }
}

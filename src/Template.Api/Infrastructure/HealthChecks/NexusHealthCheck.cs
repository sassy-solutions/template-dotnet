using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Template.Api.Application.Ports;
using Template.Api.Infrastructure.Nexus;

namespace Template.Api.Infrastructure.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Nexus platform.
/// Tagged as "ready" to be included in the readiness probe.
/// Returns Healthy when Nexus API key is not configured (graceful degradation).
/// </summary>
public class NexusHealthCheck : IHealthCheck
{
    private readonly INexusClient _nexusClient;
    private readonly NexusClientOptions _options;
    private readonly ILogger<NexusHealthCheck> _logger;

    public NexusHealthCheck(INexusClient nexusClient, IOptions<NexusClientOptions> options, ILogger<NexusHealthCheck> logger)
    {
        _nexusClient = nexusClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Skip health check when Nexus is not configured (no API key)
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            return HealthCheckResult.Healthy("Nexus not configured (no API key) — skipping");
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            var isHealthy = await _nexusClient.IsHealthyAsync(cts.Token);

            if (isHealthy)
            {
                _logger.LogDebug("Nexus health check succeeded");
                return HealthCheckResult.Healthy("Nexus is reachable");
            }

            _logger.LogWarning("Nexus health check returned unhealthy");
            return HealthCheckResult.Degraded("Nexus is not healthy");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Nexus health check was cancelled");
            return HealthCheckResult.Degraded("Nexus health check was cancelled");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Nexus health check timed out");
            return HealthCheckResult.Degraded("Nexus health check timed out after 2 seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nexus health check failed");
            return HealthCheckResult.Degraded("Nexus health check failed", ex);
        }
    }
}

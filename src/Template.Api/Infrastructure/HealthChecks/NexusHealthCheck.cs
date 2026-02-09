using Microsoft.Extensions.Diagnostics.HealthChecks;
using Template.Api.Application.Ports;

namespace Template.Api.Infrastructure.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Nexus platform.
/// Tagged as "ready" to be included in the readiness probe.
/// </summary>
public class NexusHealthCheck : IHealthCheck
{
    private readonly INexusClient _nexusClient;
    private readonly ILogger<NexusHealthCheck> _logger;

    public NexusHealthCheck(INexusClient nexusClient, ILogger<NexusHealthCheck> logger)
    {
        _nexusClient = nexusClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
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
            return HealthCheckResult.Unhealthy("Nexus is not healthy");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nexus health check failed");
            return HealthCheckResult.Unhealthy("Nexus health check failed", ex);
        }
    }
}

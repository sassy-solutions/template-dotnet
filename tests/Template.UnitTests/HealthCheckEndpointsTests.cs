using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Template.UnitTests;

/// <summary>
/// Integration tests for health check endpoints.
/// These tests verify the actual HTTP behavior of the health check endpoints.
/// </summary>
public class HealthCheckEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthCheckEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthCheckResponse()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        // In test environment, Nexus is not available, so health might be unhealthy
        response.StatusCode.Should().Match(code =>
            code == HttpStatusCode.OK || code == HttpStatusCode.ServiceUnavailable,
            "health endpoint should return 200 (all healthy) or 503 (some unhealthy)");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("status");
    }

    [Fact]
    public async Task HealthLiveEndpoint_AlwaysReturnsHealthy()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "liveness probe should always return OK when the app is running");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthReadyEndpoint_ReturnsResponse()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        // The readiness probe checks dependencies (like Nexus)
        // In test environment without real Nexus, it might fail - that's expected
        response.StatusCode.Should().Match(code =>
            code == HttpStatusCode.OK || code == HttpStatusCode.ServiceUnavailable,
            "readiness probe should check dependencies and return 200 (healthy) or 503 (unhealthy)");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("status");
    }

    [Fact]
    public async Task HealthEndpoint_IncludesAllRegisteredChecks()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // In test environment, Nexus is not available, so health might be unhealthy
        // But the response should still include all checks
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // The /health endpoint should include all registered checks
        content.Should().Contain("self", "should include the 'self' health check");
        content.Should().Contain("nexus", "should include the 'nexus' health check");
    }

    [Fact]
    public async Task HealthReadyEndpoint_DoesNotGateReadinessOnTheControlPlane()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // The SDK tags its control-plane check "nexus", deliberately NOT "ready"
        // (Nexus.Sdk NexusServiceCollectionExtensions, 2026-07-06): a tenant pod's readiness
        // must not hard-depend on the platform API. Gating on it drains traffic from healthy
        // pods on a platform blip, and the SDK's ~3 s reachability timeout can exceed the
        // probe's — pods stuck 0/1. The check still shows on /health (see above).
        content.Should().NotContain("nexus",
            "readiness must not depend on the Nexus control plane being reachable");
    }

    [Fact]
    public async Task HealthLiveEndpoint_DoesNotIncludeAnyChecks()
    {
        // Act
        var response = await _client.GetAsync("/health/live");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The /health/live endpoint should not run any checks (Predicate = _ => false)
        // It should just confirm the app is responsive
        content.Should().Contain("Healthy");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_ReturnJsonContentType(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json",
            $"health endpoint {endpoint} should return JSON");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_AreAccessibleWithoutAuthentication(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            $"health endpoint {endpoint} should be accessible without authentication for Kubernetes probes");
    }
}

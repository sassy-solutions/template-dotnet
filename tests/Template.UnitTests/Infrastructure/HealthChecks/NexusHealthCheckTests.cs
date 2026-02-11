using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Template.Api.Application.Ports;
using Template.Api.Infrastructure.HealthChecks;
using Template.Api.Infrastructure.Nexus;
using Xunit;

namespace Template.UnitTests.Infrastructure.HealthChecks;

public class NexusHealthCheckTests
{
    private readonly INexusClient _nexusClient;
    private readonly IOptions<NexusClientOptions> _options;
    private readonly ILogger<NexusHealthCheck> _logger;
    private readonly NexusHealthCheck _healthCheck;

    public NexusHealthCheckTests()
    {
        _nexusClient = Substitute.For<INexusClient>();
        _options = Options.Create(new NexusClientOptions { ApiKey = "nxs_test" });
        _logger = Substitute.For<ILogger<NexusHealthCheck>>();
        _healthCheck = new NexusHealthCheck(_nexusClient, _options, _logger);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNexusIsHealthy_ReturnsHealthy()
    {
        // Arrange
        _nexusClient.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Nexus is reachable");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNexusIsUnhealthy_ReturnsDegraded()
    {
        // Arrange
        _nexusClient.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(false);
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("not healthy");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNexusThrows_ReturnsDegraded()
    {
        // Arrange
        _nexusClient.IsHealthyAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("failed");
        result.Exception.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCancelled_ReturnsDegraded()
    {
        // Arrange
        var context = new HealthCheckContext();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _nexusClient.IsHealthyAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _healthCheck.CheckHealthAsync(context, cts.Token);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("cancelled");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiKeyNotConfigured_ReturnsHealthy()
    {
        // Arrange
        var options = Options.Create(new NexusClientOptions { ApiKey = null });
        var healthCheck = new NexusHealthCheck(_nexusClient, options, _logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("skipping");
    }
}

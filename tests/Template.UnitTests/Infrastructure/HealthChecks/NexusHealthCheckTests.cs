using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Template.Api.Application.Ports;
using Template.Api.Infrastructure.HealthChecks;
using Xunit;

namespace Template.UnitTests.Infrastructure.HealthChecks;

public class NexusHealthCheckTests
{
    private readonly INexusClient _nexusClient;
    private readonly ILogger<NexusHealthCheck> _logger;
    private readonly NexusHealthCheck _healthCheck;

    public NexusHealthCheckTests()
    {
        _nexusClient = Substitute.For<INexusClient>();
        _logger = Substitute.For<ILogger<NexusHealthCheck>>();
        _healthCheck = new NexusHealthCheck(_nexusClient, _logger);
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
    public async Task CheckHealthAsync_WhenNexusIsUnhealthy_ReturnsUnhealthy()
    {
        // Arrange
        _nexusClient.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(false);
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not healthy");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNexusThrows_ReturnsUnhealthy()
    {
        // Arrange
        _nexusClient.IsHealthyAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("failed");
        result.Exception.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCancelled_ReturnsUnhealthy()
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
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("cancelled");
    }
}

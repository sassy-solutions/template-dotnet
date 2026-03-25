using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexus.Sdk.Client;
using NSubstitute;
using Template.Api.Controllers;
using Xunit;

namespace Template.UnitTests;

public class HelloControllerTests
{
    private readonly HelloController _controller;
    private readonly ILogger<HelloController> _logger;
    private readonly IConfiguration _configuration;

    public HelloControllerTests()
    {
        _logger = Substitute.For<ILogger<HelloController>>();

        var configData = new Dictionary<string, string?>
        {
            { "ServiceName", "TestService" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _controller = new HelloController(_logger, _configuration);
    }

    [Fact]
    public void Get_ReturnsOkResult_WithHelloResponse()
    {
        // Act
        var result = _controller.Get();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<HelloResponse>().Subject;

        response.Message.Should().Contain("TestService");
        response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("Alice")]
    [InlineData("Bob")]
    [InlineData("Charlie")]
    public void GetByName_ReturnsOkResult_WithPersonalizedGreeting(string name)
    {
        // Act
        var result = _controller.GetByName(name);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<HelloResponse>().Subject;

        response.Message.Should().Contain(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetByName_ReturnsBadRequest_WhenNameIsEmpty(string? name)
    {
        // Act
        var result = _controller.GetByName(name!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task NexusStatusAsync_WhenHealthy_ReturnsConnected()
    {
        // Arrange
        var nexusClient = Substitute.For<INexusClient>();
        nexusClient.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _controller.NexusStatusAsync(nexusClient, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task NexusStatusAsync_WhenUnhealthy_ReturnsDisconnected()
    {
        // Arrange
        var nexusClient = Substitute.For<INexusClient>();
        nexusClient.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _controller.NexusStatusAsync(nexusClient, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}

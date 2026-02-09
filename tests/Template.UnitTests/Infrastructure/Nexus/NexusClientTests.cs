using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Template.Api.Infrastructure.Nexus;
using Template.Api.Infrastructure.Nexus.Dto;
using Template.UnitTests.TestHelpers;
using Xunit;

namespace Template.UnitTests.Infrastructure.Nexus;

public class NexusClientTests
{
    private readonly HttpMessageHandlerStub _handler;
    private readonly NexusClient _client;

    public NexusClientTests()
    {
        _handler = new HttpMessageHandlerStub();
        var httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("http://nexus.test/")
        };
        var logger = Substitute.For<ILogger<NexusClient>>();
        _client = new NexusClient(httpClient, logger);
    }

    [Fact]
    public async Task TrackAsync_WhenSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        var trackResponse = new TrackResponse("hello", 42, DateTimeOffset.UtcNow);
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(trackResponse),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        // Act
        var result = await _client.TrackAsync("hello");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Event.Should().Be("hello");
        result.Value.Count.Should().Be(42);
    }

    [Fact]
    public async Task TrackAsync_WhenServerError_ReturnsFailure()
    {
        // Arrange
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server error")
        };

        // Act
        var result = await _client.TrackAsync("hello");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task TrackAsync_WhenUnauthorized_ReturnsFailure()
    {
        // Arrange
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Invalid API key")
        };

        // Act
        var result = await _client.TrackAsync("hello");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Error.Should().Contain("Invalid API key");
    }

    [Fact]
    public async Task TrackAsync_SendsToCorrectEndpoint()
    {
        // Arrange
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TrackResponse("test", 1, DateTimeOffset.UtcNow)),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        // Act
        await _client.TrackAsync("test");

        // Assert
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/v1/track");
        _handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task GetUsageAsync_WhenSuccessful_ReturnsUsageResponse()
    {
        // Arrange
        var usage = new UsageResponse("org-1", 100, [new UsageEvent("hello", 50)]);
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(usage),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        // Act
        var result = await _client.GetUsageAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalEvents.Should().Be(100);
        result.Value.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUsageAsync_SendsToCorrectEndpoint()
    {
        // Arrange
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new UsageResponse("org-1", 0, [])),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        // Act
        await _client.GetUsageAsync();

        // Assert
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/v1/usage");
        _handler.LastRequest.Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task GetUsageByEventAsync_EncodesEventNameInUrl()
    {
        // Arrange
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TrackResponse("hello.world", 5, DateTimeOffset.UtcNow)),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        // Act
        await _client.GetUsageByEventAsync("hello.world");

        // Assert
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/v1/usage/hello.world");
    }

    [Fact]
    public async Task IsHealthyAsync_WhenOk_ReturnsTrue()
    {
        // Arrange
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        // Act
        var result = await _client.IsHealthyAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenError_ReturnsFalse()
    {
        // Arrange
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        // Act
        var result = await _client.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenException_ReturnsFalse()
    {
        // Arrange
        _handler.Exception = new HttpRequestException("Connection refused");

        // Act
        var result = await _client.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_SendsToCorrectEndpoint()
    {
        // Arrange
        _handler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        // Act
        await _client.IsHealthyAsync();

        // Assert
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/v1/health");
    }

    [Fact]
    public async Task TrackAsync_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _handler.Exception = new HttpRequestException("DNS failure");

        // Act
        var result = await _client.TrackAsync("test");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(0);
        result.Error.Should().Contain("DNS failure");
    }
}

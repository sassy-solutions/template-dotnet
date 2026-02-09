using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Template.Api.Infrastructure.Nexus;
using Template.UnitTests.TestHelpers;
using Xunit;

namespace Template.UnitTests.Infrastructure.Nexus;

public class NexusApiKeyHandlerTests
{
    [Fact]
    public async Task SendAsync_WhenApiKeyConfigured_AddsHeader()
    {
        // Arrange
        var options = Options.Create(new NexusClientOptions { ApiKey = "nxs_test123" });
        var innerHandler = new HttpMessageHandlerStub
        {
            ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        };
        var handler = new NexusApiKeyHandler(options)
        {
            InnerHandler = innerHandler
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };

        // Act
        await client.GetAsync("api/v1/health");

        // Assert
        innerHandler.LastRequest!.Headers.Should()
            .ContainKey("X-Api-Key")
            .WhoseValue.Should().ContainSingle("nxs_test123");
    }

    [Fact]
    public async Task SendAsync_WhenApiKeyEmpty_SkipsHeader()
    {
        // Arrange
        var options = Options.Create(new NexusClientOptions { ApiKey = "" });
        var innerHandler = new HttpMessageHandlerStub
        {
            ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        };
        var handler = new NexusApiKeyHandler(options)
        {
            InnerHandler = innerHandler
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };

        // Act
        await client.GetAsync("api/v1/health");

        // Assert
        innerHandler.LastRequest!.Headers.Should().NotContainKey("X-Api-Key");
    }

    [Fact]
    public async Task SendAsync_WhenApiKeyNull_SkipsHeader()
    {
        // Arrange
        var options = Options.Create(new NexusClientOptions { ApiKey = null });
        var innerHandler = new HttpMessageHandlerStub
        {
            ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        };
        var handler = new NexusApiKeyHandler(options)
        {
            InnerHandler = innerHandler
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };

        // Act
        await client.GetAsync("api/v1/health");

        // Assert
        innerHandler.LastRequest!.Headers.Should().NotContainKey("X-Api-Key");
    }
}

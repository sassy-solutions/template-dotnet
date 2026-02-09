using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Template.Api.Application.Attributes;
using Template.Api.Application.Filters;
using Template.Api.Application.Ports;
using Template.Api.Infrastructure.Nexus.Dto;
using Xunit;

namespace Template.UnitTests.Application.Filters;

public class NexusTrackFilterTests
{
    private readonly INexusClient _nexusClient;
    private readonly NexusTrackFilter _filter;

    public NexusTrackFilterTests()
    {
        _nexusClient = Substitute.For<INexusClient>();
        var logger = Substitute.For<ILogger<NexusTrackFilter>>();
        _filter = new NexusTrackFilter(_nexusClient, logger);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithNexusTrack_TracksOnSuccess()
    {
        // Arrange
        var trackAttr = new NexusTrackAttribute("hello");
        var (context, next) = CreateFilterContext(trackAttr, new OkObjectResult("ok"));

        _nexusClient.TrackAsync("hello", null, Arg.Any<CancellationToken>())
            .Returns(NexusResult<TrackResponse>.Success(
                new TrackResponse("hello", 1, DateTimeOffset.UtcNow)));

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Give fire-and-forget time to execute
        await Task.Delay(100);

        // Assert
        await _nexusClient.Received(1).TrackAsync("hello", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutAttribute_DoesNotTrack()
    {
        // Arrange
        var (context, next) = CreateFilterContext(attribute: null, new OkObjectResult("ok"));

        // Act
        await _filter.OnActionExecutionAsync(context, next);
        await Task.Delay(100);

        // Assert
        await _nexusClient.DidNotReceive()
            .TrackAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_OnErrorResponse_SkipsTrackingByDefault()
    {
        // Arrange
        var trackAttr = new NexusTrackAttribute("hello");
        var (context, next) = CreateFilterContext(trackAttr, new BadRequestObjectResult("error"));

        // Act
        await _filter.OnActionExecutionAsync(context, next);
        await Task.Delay(100);

        // Assert
        await _nexusClient.DidNotReceive()
            .TrackAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_OnErrorResponse_TracksWhenTrackOnFailureEnabled()
    {
        // Arrange
        var trackAttr = new NexusTrackAttribute("hello") { TrackOnFailure = true };
        var (context, next) = CreateFilterContext(trackAttr, new BadRequestObjectResult("error"));

        _nexusClient.TrackAsync("hello", null, Arg.Any<CancellationToken>())
            .Returns(NexusResult<TrackResponse>.Success(
                new TrackResponse("hello", 1, DateTimeOffset.UtcNow)));

        // Act
        await _filter.OnActionExecutionAsync(context, next);
        await Task.Delay(100);

        // Assert
        await _nexusClient.Received(1).TrackAsync("hello", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenTrackingFails_DoesNotThrow()
    {
        // Arrange
        var trackAttr = new NexusTrackAttribute("hello");
        var (context, next) = CreateFilterContext(trackAttr, new OkObjectResult("ok"));

        _nexusClient.TrackAsync("hello", null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act & Assert — should not throw
        await _filter.OnActionExecutionAsync(context, next);
        await Task.Delay(100);
    }

    private static (ActionExecutingContext context, ActionExecutionDelegate next) CreateFilterContext(
        NexusTrackAttribute? attribute,
        IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();

        var metadata = new List<object>();
        if (attribute is not null)
        {
            metadata.Add(attribute);
        }

        var actionDescriptor = new ActionDescriptor
        {
            EndpointMetadata = metadata
        };

        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);
        var filters = new List<IFilterMetadata>();

        var context = new ActionExecutingContext(
            actionContext,
            filters,
            new Dictionary<string, object?>(),
            controller: null!);

        var executedContext = new ActionExecutedContext(actionContext, filters, controller: null!)
        {
            Result = result
        };

        return (context, NextAsync);

        Task<ActionExecutedContext> NextAsync()
        {
            return Task.FromResult(executedContext);
        }
    }
}

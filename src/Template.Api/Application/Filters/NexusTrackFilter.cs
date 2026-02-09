using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Template.Api.Application.Attributes;
using Template.Api.Application.Ports;

namespace Template.Api.Application.Filters;

/// <summary>
/// Global action filter that processes [NexusTrack] attributes.
/// After successful action execution, tracks the event in Nexus (fire-and-forget).
/// Tracking failures are logged but never affect the HTTP response.
/// </summary>
public sealed class NexusTrackFilter : IAsyncActionFilter
{
    private readonly INexusClient _nexusClient;
    private readonly ILogger<NexusTrackFilter> _logger;

    public NexusTrackFilter(INexusClient nexusClient, ILogger<NexusTrackFilter> logger)
    {
        _nexusClient = nexusClient;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executedContext = await next();

        var trackAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<NexusTrackAttribute>()
            .FirstOrDefault();

        if (trackAttr is null)
        {
            return;
        }

        var isSuccess = executedContext.Exception is null
            && IsSuccessStatusCode(executedContext.Result);

        if (!isSuccess && !trackAttr.TrackOnFailure)
        {
            return;
        }

        // Fire-and-forget — tracking must never delay the response
        _ = TrackEventAsync(trackAttr.EventName);
    }

    private async Task TrackEventAsync(string eventName)
    {
        try
        {
            await _nexusClient.TrackAsync(eventName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track event {Event} in Nexus", eventName);
        }
    }

    private static bool IsSuccessStatusCode(IActionResult? result) =>
        result switch
        {
            ObjectResult obj => obj.StatusCode is null or (>= 200 and < 400),
            StatusCodeResult sc => sc.StatusCode is >= 200 and < 400,
            _ => result is not null
        };
}

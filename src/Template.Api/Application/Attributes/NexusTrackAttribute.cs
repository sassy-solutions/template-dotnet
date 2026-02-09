namespace Template.Api.Application.Attributes;

/// <summary>
/// Marks an endpoint for Nexus usage tracking.
/// When applied, successful requests automatically track the event in Nexus.
/// Tracking is fire-and-forget and never affects the response.
/// </summary>
/// <example>
/// [NexusTrack("order.created")]
/// [HttpPost]
/// public IActionResult CreateOrder(Order order) { ... }
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NexusTrackAttribute : Attribute
{
    /// <summary>The event name to track in Nexus.</summary>
    public string EventName { get; }

    /// <summary>If true, tracks even when the action returns an error response.</summary>
    public bool TrackOnFailure { get; set; }

    public NexusTrackAttribute(string eventName)
    {
        EventName = eventName;
    }
}

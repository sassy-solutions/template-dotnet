namespace Template.Api.Infrastructure.Nexus.Dto;

/// <summary>Request model for tracking an event.</summary>
public sealed record TrackRequest(string Event, Dictionary<string, object>? Metadata = null);

/// <summary>Response model for a tracked event with its current count.</summary>
public sealed record TrackResponse(string Event, long Count, DateTimeOffset Timestamp);

/// <summary>Usage statistics for an organization.</summary>
public sealed record UsageResponse(string OrganizationId, long TotalEvents, UsageEvent[] Events);

/// <summary>A single usage event counter.</summary>
public sealed record UsageEvent(string Event, long Count);

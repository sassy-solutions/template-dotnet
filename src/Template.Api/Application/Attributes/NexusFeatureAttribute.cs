namespace Template.Api.Application.Attributes;

/// <summary>
/// Gates an endpoint behind a Nexus feature flag.
/// Returns 404 if the feature is disabled for the current organization.
/// </summary>
/// <remarks>
/// Requires Nexus feature flag API (planned — this attribute is a placeholder).
/// When the API is available, add a NexusFeatureFilter to evaluate this attribute.
/// </remarks>
/// <example>
/// [NexusFeature("premium_analytics")]
/// [HttpGet("analytics")]
/// public IActionResult GetAnalytics() { ... }
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NexusFeatureAttribute : Attribute
{
    /// <summary>The feature flag name in Nexus.</summary>
    public string FeatureName { get; }

    public NexusFeatureAttribute(string featureName) => FeatureName = featureName;
}

namespace Template.Api.Application.Attributes;

/// <summary>
/// Enforces Nexus-based authorization on an endpoint.
/// Validates that the caller has the required role or permission via Nexus.
/// </summary>
/// <remarks>
/// Requires Nexus authorization API (planned — this attribute is a placeholder).
/// When the API is available, add a NexusAuthorizeFilter to evaluate this attribute.
///
/// Pattern: like [Authorize(Roles = "Admin")] but delegated to Nexus.
/// </remarks>
/// <example>
/// [NexusAuthorize(Role = "manager")]
/// [HttpDelete("{id}")]
/// public IActionResult DeleteItem(int id) { ... }
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NexusAuthorizeAttribute : Attribute
{
    /// <summary>Required role (e.g. "admin", "manager", "viewer").</summary>
    public string? Role { get; set; }

    /// <summary>Required permission (e.g. "orders:write", "reports:read").</summary>
    public string? Permission { get; set; }
}

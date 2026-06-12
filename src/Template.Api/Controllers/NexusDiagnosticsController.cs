using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Attributes;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Models;
using Nexus.Sdk.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Template.Api.Controllers;

/// <summary>
/// Diagnostics probes for end-to-end platform validation.
/// Lets the Nexus platform (or an operator) verify that configuration values
/// and feature flags flow correctly from Nexus into this application.
///
/// SECURITY: the whole controller is gated behind the configuration key
/// <c>Nexus:Diagnostics:Enabled</c> (default <c>false</c>). When disabled,
/// every endpoint returns 404 so production apps never expose config values.
/// Enable it per environment by setting the platform config
/// <c>Nexus__Diagnostics__Enabled = true</c> (injected via the deploy env Secret).
/// </summary>
[ApiController]
[Route("probe")]
public class NexusDiagnosticsController : ControllerBase
{
    private const string _enabledConfigKey = "Nexus:Diagnostics:Enabled";

    private readonly IConfiguration _configuration;
    private readonly IFeatureService _featureService;
    private readonly INexusClient _nexusClient;
    private readonly NexusOptions _nexusOptions;
    private readonly ILogger<NexusDiagnosticsController> _logger;

    public NexusDiagnosticsController(
        IConfiguration configuration,
        IFeatureService featureService,
        INexusClient nexusClient,
        IOptions<NexusOptions> nexusOptions,
        ILogger<NexusDiagnosticsController> logger)
    {
        _configuration = configuration;
        _featureService = featureService;
        _nexusClient = nexusClient;
        _nexusOptions = nexusOptions.Value;
        _logger = logger;
    }

    private bool DiagnosticsEnabled => _configuration.GetValue(_enabledConfigKey, false);

    /// <summary>
    /// Probes a configuration key as seen by this application's IConfiguration.
    /// </summary>
    /// <param name="key">
    /// Configuration key. Use <c>__</c> as the section separator (it is mapped
    /// to <c>:</c>), mirroring environment-variable naming — e.g.
    /// <c>Foo__Bar</c> probes <c>Foo:Bar</c>. This makes a platform config /
    /// secret named <c>Foo__Bar</c> (injected through the deploy env Secret)
    /// directly probeable by its own name.
    /// </param>
    /// <response code="200">Probe result with the resolved value (if any).</response>
    /// <response code="404">Diagnostics are disabled (default).</response>
    [HttpGet("config/{key}")]
    [NexusTrack("diagnostics.config-probe")]
    [SwaggerOperation(
        Summary = "Probe a configuration key",
        Description = "Returns whether the key resolves through IConfiguration and its value. Gated by Nexus:Diagnostics:Enabled (404 when disabled).",
        OperationId = "ProbeConfig",
        Tags = ["Diagnostics"]
    )]
    [ProducesResponseType<ConfigProbeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ProbeConfig(string key)
    {
        if (!DiagnosticsEnabled)
        {
            return NotFound();
        }

        var configKey = key.Replace("__", ":");
        var value = _configuration[configKey];

        _logger.LogInformation(
            "Diagnostics config probe for {ConfigKey}: found={Found}", configKey, value is not null);

        return Ok(new ConfigProbeResponse
        {
            Key = configKey,
            Found = value is not null,
            Value = value
        });
    }

    /// <summary>
    /// Probes a feature flag through the Nexus SDK, auto-registering it on
    /// first evaluation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why explicit registration: <c>IFeatureService.GetConfigAsync</c> alone
    /// does NOT register unknown flags — the SDK endpoint
    /// <c>GET /api/v1/sdk/features/{key}</c> returns 404 for unknown keys and the
    /// SDK then falls back to <c>DefaultFeatureEnabled</c>. Flags are only
    /// auto-created (disabled by default) through <c>POST /api/v1/sdk/register</c>,
    /// which the SDK normally calls once at startup for <c>[NexusFeature]</c> /
    /// <c>[NexusTrack]</c> attributes discovered by assembly scanning.
    /// </para>
    /// <para>
    /// This probe therefore calls <c>INexusClient.RegisterAsync</c> with the
    /// probed key (idempotent server-side), caches the returned config via
    /// <c>IFeatureService.SetBulk</c>, then evaluates through the same
    /// <c>GetConfigAsync</c> path the <c>[NexusFeature]</c> gate uses —
    /// demonstrating dynamic flag registration end-to-end.
    /// </para>
    /// </remarks>
    /// <param name="flag">Feature flag key to probe.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Probe result with the evaluated enabled state.</response>
    /// <response code="404">Diagnostics are disabled (default).</response>
    [HttpGet("flags/{flag}")]
    [NexusTrack("diagnostics.flag-probe")]
    [SwaggerOperation(
        Summary = "Probe a feature flag (auto-registers it in Nexus)",
        Description = "Registers the flag with Nexus when unknown (created disabled by default), then evaluates it through the SDK. Gated by Nexus:Diagnostics:Enabled (404 when disabled).",
        OperationId = "ProbeFlag",
        Tags = ["Diagnostics"]
    )]
    [ProducesResponseType<FlagProbeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProbeFlagAsync(string flag, CancellationToken cancellationToken)
    {
        if (!DiagnosticsEnabled)
        {
            return NotFound();
        }

        // 1. Dynamic registration: POST /api/v1/sdk/register auto-creates the
        //    flag in Nexus when it does not exist yet (disabled by default,
        //    idempotent when it does) and returns its authoritative config.
        var registered = false;
        var registration = await _nexusClient.RegisterAsync(
            new SdkRegistrationRequest(
                _nexusOptions.ApplicationId,
                _nexusOptions.Environment,
                _nexusOptions.Version,
                [new FeatureRegistration(flag, $"{nameof(NexusDiagnosticsController)}.{nameof(ProbeFlagAsync)}")],
                []),
            cancellationToken);

        if (registration?.Features is { Count: > 0 })
        {
            _featureService.SetBulk(registration.Features);
            registered = registration.Features.ContainsKey(flag);
        }

        // 2. Evaluate through the exact path the [NexusFeature] gate uses.
        //    When Nexus is unreachable this falls back to DefaultFeatureEnabled.
        var config = await _featureService.GetConfigAsync(flag, cancellationToken);
        var enabled = config?.Enabled ?? false;

        _logger.LogInformation(
            "Diagnostics flag probe for {Flag}: enabled={Enabled} registered={Registered}",
            flag, enabled, registered);

        return Ok(new FlagProbeResponse
        {
            Flag = flag,
            Enabled = enabled,
            Registered = registered
        });
    }
}

/// <summary>Result of a configuration probe.</summary>
[SwaggerSchema(Description = "Result of probing a configuration key through IConfiguration")]
public record ConfigProbeResponse
{
    /// <summary>The effective configuration key that was probed (after __ to : mapping).</summary>
    public required string Key { get; init; }

    /// <summary>Whether the key resolved to a non-null value.</summary>
    public required bool Found { get; init; }

    /// <summary>The resolved value, or null when not found.</summary>
    public string? Value { get; init; }
}

/// <summary>Result of a feature flag probe.</summary>
[SwaggerSchema(Description = "Result of probing a feature flag through the Nexus SDK")]
public record FlagProbeResponse
{
    /// <summary>The feature flag key that was probed.</summary>
    public required string Flag { get; init; }

    /// <summary>The evaluated enabled state (SDK default fallback applies when Nexus is unreachable).</summary>
    public required bool Enabled { get; init; }

    /// <summary>Whether the flag was confirmed registered in Nexus during this probe.</summary>
    public required bool Registered { get; init; }
}

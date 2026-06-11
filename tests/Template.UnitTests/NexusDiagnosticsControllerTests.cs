using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Models;
using Nexus.Sdk.Services;
using NSubstitute;
using Template.Api.Controllers;
using Xunit;

namespace Template.UnitTests;

public class NexusDiagnosticsControllerTests
{
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly INexusClient _nexusClient = Substitute.For<INexusClient>();
    private readonly ILogger<NexusDiagnosticsController> _logger =
        Substitute.For<ILogger<NexusDiagnosticsController>>();

    private NexusDiagnosticsController CreateController(
        bool diagnosticsEnabled,
        Dictionary<string, string?>? extraConfig = null)
    {
        var configData = new Dictionary<string, string?>
        {
            { "Nexus:Diagnostics:Enabled", diagnosticsEnabled ? "true" : "false" }
        };

        if (extraConfig is not null)
        {
            foreach (var (key, value) in extraConfig)
            {
                configData[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var options = Options.Create(new NexusOptions
        {
            ApplicationId = "test-app",
            Environment = "dev",
            Version = "1.0.0"
        });

        return new NexusDiagnosticsController(
            configuration, _featureService, _nexusClient, options, _logger);
    }

    // =========================
    // Gating — default OFF
    // =========================

    [Fact]
    public void ProbeConfig_WhenDiagnosticsDisabled_Returns404()
    {
        var controller = CreateController(diagnosticsEnabled: false,
            extraConfig: new Dictionary<string, string?> { { "Some:Key", "value" } });

        var result = controller.ProbeConfig("Some__Key");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void ProbeConfig_WhenGateKeyMissing_Returns404()
    {
        // No Nexus:Diagnostics:Enabled entry at all — must default to disabled.
        var configuration = new ConfigurationBuilder().Build();
        var controller = new NexusDiagnosticsController(
            configuration, _featureService, _nexusClient,
            Options.Create(new NexusOptions()), _logger);

        var result = controller.ProbeConfig("AnyKey");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ProbeFlag_WhenDiagnosticsDisabled_Returns404()
    {
        var controller = CreateController(diagnosticsEnabled: false);

        var result = await controller.ProbeFlagAsync("my-flag", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _nexusClient.DidNotReceiveWithAnyArgs().RegisterAsync(default!, default);
    }

    // =========================
    // Config probe
    // =========================

    [Fact]
    public void ProbeConfig_WhenKeyExists_ReturnsFoundWithValue()
    {
        var controller = CreateController(diagnosticsEnabled: true,
            extraConfig: new Dictionary<string, string?> { { "Probe:Smoke", "it-works" } });

        var result = controller.ProbeConfig("Probe__Smoke");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ConfigProbeResponse>().Subject;
        response.Key.Should().Be("Probe:Smoke");
        response.Found.Should().BeTrue();
        response.Value.Should().Be("it-works");
    }

    [Fact]
    public void ProbeConfig_WhenKeyMissing_ReturnsFoundFalse()
    {
        var controller = CreateController(diagnosticsEnabled: true);

        var result = controller.ProbeConfig("Does__Not__Exist");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ConfigProbeResponse>().Subject;
        response.Key.Should().Be("Does:Not:Exist");
        response.Found.Should().BeFalse();
        response.Value.Should().BeNull();
    }

    [Fact]
    public void ProbeConfig_WithFlatKey_ProbesItVerbatim()
    {
        var controller = CreateController(diagnosticsEnabled: true,
            extraConfig: new Dictionary<string, string?> { { "FLAT_KEY", "flat" } });

        var result = controller.ProbeConfig("FLAT_KEY");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ConfigProbeResponse>().Subject;
        response.Found.Should().BeTrue();
        response.Value.Should().Be("flat");
    }

    // =========================
    // Flag probe — dynamic registration
    // =========================

    [Fact]
    public async Task ProbeFlag_WhenNexusReturnsConfig_RegistersAndEvaluates()
    {
        var flagConfig = new FeatureConfig("e2e-flag", Enabled: true, null, null, null);
        _nexusClient
            .RegisterAsync(Arg.Any<SdkRegistrationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SdkRegistrationResponse(new Dictionary<string, FeatureConfig>
            {
                ["e2e-flag"] = flagConfig
            }));
        _featureService
            .GetConfigAsync("e2e-flag", Arg.Any<CancellationToken>())
            .Returns(flagConfig);

        var controller = CreateController(diagnosticsEnabled: true);

        var result = await controller.ProbeFlagAsync("e2e-flag", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<FlagProbeResponse>().Subject;
        response.Flag.Should().Be("e2e-flag");
        response.Enabled.Should().BeTrue();
        response.Registered.Should().BeTrue();

        // The probed key must be sent through the SDK registration endpoint
        // (that is what auto-creates unknown flags server-side).
        await _nexusClient.Received(1).RegisterAsync(
            Arg.Is<SdkRegistrationRequest>(r =>
                r.Features.Count == 1 && r.Features[0].Key == "e2e-flag"),
            Arg.Any<CancellationToken>());

        // And the returned config must be cached so subsequent evaluations hit it.
        _featureService.Received(1).SetBulk(
            Arg.Is<Dictionary<string, FeatureConfig>>(d => d.ContainsKey("e2e-flag")));
    }

    [Fact]
    public async Task ProbeFlag_WhenFlagAutoCreatedDisabled_ReturnsEnabledFalse()
    {
        // Nexus auto-creates unknown flags DISABLED by default.
        var flagConfig = new FeatureConfig("new-flag", Enabled: false, null, null, null);
        _nexusClient
            .RegisterAsync(Arg.Any<SdkRegistrationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SdkRegistrationResponse(new Dictionary<string, FeatureConfig>
            {
                ["new-flag"] = flagConfig
            }));
        _featureService
            .GetConfigAsync("new-flag", Arg.Any<CancellationToken>())
            .Returns(flagConfig);

        var controller = CreateController(diagnosticsEnabled: true);

        var result = await controller.ProbeFlagAsync("new-flag", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<FlagProbeResponse>().Subject;
        response.Enabled.Should().BeFalse();
        response.Registered.Should().BeTrue();
    }

    [Fact]
    public async Task ProbeFlag_WhenNexusUnreachable_FallsBackToFeatureServiceDefault()
    {
        // RegisterAsync returning null mirrors the SDK client behavior when
        // the Nexus API is unreachable.
        _nexusClient
            .RegisterAsync(Arg.Any<SdkRegistrationRequest>(), Arg.Any<CancellationToken>())
            .Returns((SdkRegistrationResponse?)null);
        _featureService
            .GetConfigAsync("offline-flag", Arg.Any<CancellationToken>())
            .Returns(new FeatureConfig("offline-flag", Enabled: true, null, null, null));

        var controller = CreateController(diagnosticsEnabled: true);

        var result = await controller.ProbeFlagAsync("offline-flag", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<FlagProbeResponse>().Subject;
        response.Enabled.Should().BeTrue();
        response.Registered.Should().BeFalse();
        _featureService.DidNotReceiveWithAnyArgs().SetBulk(default!);
    }

    [Fact]
    public async Task ProbeFlag_WhenNoConfigResolves_ReturnsEnabledFalse()
    {
        _nexusClient
            .RegisterAsync(Arg.Any<SdkRegistrationRequest>(), Arg.Any<CancellationToken>())
            .Returns((SdkRegistrationResponse?)null);
        _featureService
            .GetConfigAsync("ghost-flag", Arg.Any<CancellationToken>())
            .Returns((FeatureConfig?)null);

        var controller = CreateController(diagnosticsEnabled: true);

        var result = await controller.ProbeFlagAsync("ghost-flag", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<FlagProbeResponse>().Subject;
        response.Enabled.Should().BeFalse();
        response.Registered.Should().BeFalse();
    }
}

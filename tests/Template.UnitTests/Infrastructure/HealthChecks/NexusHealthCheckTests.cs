using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Net;
using Template.Api.Infrastructure.HealthChecks;
using Xunit;

namespace Template.UnitTests.Infrastructure.HealthChecks;

public class NexusHealthCheckTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NexusHealthCheck> _logger;
    private readonly NexusHealthCheck _healthCheck;
    private readonly HttpMessageHandlerStub _messageHandler;

    public NexusHealthCheckTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<NexusHealthCheck>>();
        _messageHandler = new HttpMessageHandlerStub();

        var httpClient = new HttpClient(_messageHandler)
        {
            BaseAddress = new Uri("http://nexus.test")
        };

        _httpClientFactory.CreateClient("Nexus").Returns(httpClient);
        _healthCheck = new NexusHealthCheck(_httpClientFactory, _logger);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNexusIsReachable_ReturnsHealthy()
    {
        // Arrange
        _messageHandler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Nexus is reachable");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNexusReturnsNonSuccessStatusCode_ReturnsUnhealthy()
    {
        // Arrange
        _messageHandler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("503");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNexusIsUnreachable_ReturnsUnhealthy()
    {
        // Arrange
        _messageHandler.Exception = new HttpRequestException("Connection refused");
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Nexus is unreachable");
        result.Exception.Should().NotBeNull();
        result.Exception.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRequestTimesOut_ReturnsUnhealthy()
    {
        // Arrange
        _messageHandler.DelayResponse = TimeSpan.FromSeconds(5); // Longer than the 2s timeout
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("timed out");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCancellationRequested_ReturnsUnhealthy()
    {
        // Arrange
        _messageHandler.DelayResponse = TimeSpan.FromSeconds(10);
        var context = new HealthCheckContext();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var result = await _healthCheck.CheckHealthAsync(context, cts.Token);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("cancelled");
    }

    [Fact]
    public async Task CheckHealthAsync_CallsCorrectEndpoint()
    {
        // Arrange
        _messageHandler.ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        var context = new HealthCheckContext();

        // Act
        await _healthCheck.CheckHealthAsync(context);

        // Assert
        _messageHandler.LastRequest.Should().NotBeNull();
        _messageHandler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/health");
    }

    /// <summary>
    /// Test double for HttpMessageHandler to intercept HTTP requests in tests
    /// </summary>
    private class HttpMessageHandlerStub : HttpMessageHandler
    {
        public HttpResponseMessage? ResponseMessage { get; set; }
        public Exception? Exception { get; set; }
        public TimeSpan DelayResponse { get; set; } = TimeSpan.Zero;
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (DelayResponse > TimeSpan.Zero)
            {
                await Task.Delay(DelayResponse, cancellationToken);
            }

            if (Exception != null)
            {
                throw Exception;
            }

            return ResponseMessage ?? new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

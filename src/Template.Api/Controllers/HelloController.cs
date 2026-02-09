using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Template.Api.Application.Attributes;
using Template.Api.Application.Ports;

namespace Template.Api.Controllers;

/// <summary>
/// Hello World controller - demonstrates a basic API endpoint.
/// Replace this with your actual business logic.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HelloController : ControllerBase
{
    private readonly ILogger<HelloController> _logger;
    private readonly IConfiguration _configuration;

    public HelloController(ILogger<HelloController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Returns a hello world message.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/hello
    ///
    /// </remarks>
    /// <response code="200">Returns the greeting message with timestamp</response>
    [HttpGet]
    [NexusTrack("hello")]
    [SwaggerOperation(
        Summary = "Get hello message",
        Description = "Returns a simple hello world message with service information",
        OperationId = "GetHello",
        Tags = ["Hello"]
    )]
    [ProducesResponseType<HelloResponse>(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var serviceName = _configuration["ServiceName"] ?? "Template.Api";

        _logger.LogInformation("Hello endpoint called for service {ServiceName}", serviceName);

        return Ok(new HelloResponse
        {
            Message = $"Hello from {serviceName}!",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }

    /// <summary>
    /// Returns a personalized greeting.
    /// </summary>
    /// <param name="name">Name to greet (must not be empty or whitespace)</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/hello/John
    ///
    /// </remarks>
    /// <response code="200">Returns the personalized greeting</response>
    /// <response code="400">If the name parameter is empty or whitespace</response>
    [HttpGet("{name}")]
    [NexusTrack("hello.personalized")]
    [SwaggerOperation(
        Summary = "Get personalized hello message",
        Description = "Returns a greeting message personalized with the provided name",
        OperationId = "GetHelloByName",
        Tags = ["Hello"]
    )]
    [ProducesResponseType<HelloResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "Name cannot be empty" });
        }

        _logger.LogInformation("Personalized hello for {Name}", name);

        return Ok(new HelloResponse
        {
            Message = $"Hello, {name}!",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }
    /// <summary>
    /// Diagnostic: verifies Nexus connectivity and returns usage stats.
    /// Demonstrates direct INexusClient port usage for orchestration scenarios.
    /// </summary>
    [HttpGet("nexus-status")]
    [SwaggerOperation(
        Summary = "Check Nexus connectivity",
        Description = "Verifies the service can communicate with Nexus and returns usage statistics. Shows direct port usage pattern.",
        OperationId = "GetNexusStatus",
        Tags = ["Hello"]
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> NexusStatusAsync(
        [FromServices] INexusClient nexusClient,
        CancellationToken cancellationToken)
    {
        var healthy = await nexusClient.IsHealthyAsync(cancellationToken);
        var usage = healthy
            ? await nexusClient.GetUsageAsync(cancellationToken)
            : null;

        return Ok(new
        {
            nexusConnected = healthy,
            usage = usage?.IsSuccess == true ? usage.Value : null,
        });
    }
}

/// <summary>
/// Response model for hello endpoints.
/// </summary>
/// <example>
/// {
///   "message": "Hello from Template.Api!",
///   "timestamp": "2024-02-02T10:30:00Z",
///   "environment": "Development"
/// }
/// </example>
[SwaggerSchema(Description = "Response containing a greeting message with service metadata")]
public record HelloResponse
{
    /// <summary>
    /// The greeting message.
    /// </summary>
    /// <example>Hello from Template.Api!</example>
    [SwaggerSchema(Description = "The personalized or default greeting message")]
    public required string Message { get; init; }

    /// <summary>
    /// UTC timestamp of the response.
    /// </summary>
    /// <example>2024-02-02T10:30:00Z</example>
    [SwaggerSchema(Description = "ISO 8601 formatted UTC timestamp when the response was generated")]
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Current environment (Development, Staging, Production).
    /// </summary>
    /// <example>Development</example>
    [SwaggerSchema(Description = "The environment where the service is running")]
    public required string Environment { get; init; }
}

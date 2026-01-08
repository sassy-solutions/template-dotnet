using Microsoft.AspNetCore.Mvc;

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
    [HttpGet]
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
    /// <param name="name">Name to greet</param>
    [HttpGet("{name}")]
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
}

/// <summary>
/// Response model for hello endpoints.
/// </summary>
public record HelloResponse
{
    /// <summary>
    /// The greeting message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// UTC timestamp of the response.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Current environment (Development, Staging, Production).
    /// </summary>
    public required string Environment { get; init; }
}

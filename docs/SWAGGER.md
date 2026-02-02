# Swagger/OpenAPI Documentation

This service provides comprehensive API documentation using Swagger/OpenAPI 3.0.

## Accessing Documentation

### Development Environment

When running in **Development** mode, the Swagger UI is available at:

```
http://localhost:8080/swagger
```

The OpenAPI JSON specification is available at:

```
http://localhost:8080/swagger/v1/swagger.json
```

### Staging Environment

The Swagger UI is enabled in **Staging** for testing and validation.

### Production Environment

In **Production**, the Swagger UI is **disabled** for security reasons, but the OpenAPI JSON specification remains available for:
- API client generation
- Integration testing
- Documentation generation

## Configuration

Swagger UI visibility is controlled via `appsettings.{Environment}.json`:

```json
{
  "Swagger": {
    "EnableUI": true  // false in Production, true in Development/Staging
  }
}
```

## Features

### Included in Documentation

✅ All controller endpoints (`/api/**`)
✅ Request/response models with XML comments
✅ HTTP status codes (200, 400, 500, etc.)
✅ Request parameter descriptions
✅ Example requests and responses
✅ Model validation rules

### Excluded from Documentation

❌ Health check endpoints (`/health`, `/health/live`, `/health/ready`)
❌ Metrics endpoint (`/metrics`)
❌ Root endpoint (`/`)

These operational endpoints are intentionally excluded as they're for monitoring systems, not API consumers.

## Generating API Clients

You can generate strongly-typed API clients from the OpenAPI specification:

### TypeScript/JavaScript

```bash
# Using OpenAPI Generator
npx @openapitools/openapi-generator-cli generate \
  -i http://localhost:8080/swagger/v1/swagger.json \
  -g typescript-axios \
  -o ./generated/api-client

# Using Swagger Codegen
npx swagger-codegen-cli generate \
  -i http://localhost:8080/swagger/v1/swagger.json \
  -l typescript-axios \
  -o ./generated/api-client
```

### C#

```bash
# Using NSwag
nswag openapi2csclient \
  /input:http://localhost:8080/swagger/v1/swagger.json \
  /output:TemplateApiClient.cs \
  /namespace:Template.Client

# Using AutoRest
autorest \
  --input-file=http://localhost:8080/swagger/v1/swagger.json \
  --csharp \
  --namespace=Template.Client \
  --output-folder=./generated
```

### Python

```bash
openapi-generator-cli generate \
  -i http://localhost:8080/swagger/v1/swagger.json \
  -g python \
  -o ./generated/python-client
```

## Writing Documentation

### Controller Endpoints

Use XML comments and Swagger annotations:

```csharp
/// <summary>
/// Brief description of the endpoint.
/// </summary>
/// <param name="id">Parameter description</param>
/// <remarks>
/// Extended documentation and examples:
///
///     GET /api/resource/123
///
/// </remarks>
/// <response code="200">Successful response description</response>
/// <response code="404">Not found description</response>
[HttpGet("{id}")]
[SwaggerOperation(
    Summary = "Short summary",
    Description = "Longer description with usage context",
    OperationId = "GetById",
    Tags = new[] { "Resource" }
)]
[ProducesResponseType<ResourceDto>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public IActionResult GetById(int id)
{
    // Implementation
}
```

### Response Models

Document model properties:

```csharp
/// <summary>
/// Represents a resource in the system.
/// </summary>
public record ResourceDto
{
    /// <summary>
    /// Unique identifier for the resource.
    /// </summary>
    /// <example>12345</example>
    public int Id { get; init; }

    /// <summary>
    /// Human-readable name of the resource.
    /// </summary>
    /// <example>My Resource</example>
    public required string Name { get; init; }

    /// <summary>
    /// ISO 8601 timestamp when the resource was created.
    /// </summary>
    /// <example>2026-02-02T10:30:00Z</example>
    public DateTime CreatedAt { get; init; }
}
```

## Best Practices

### DO:
- ✅ Write clear, concise summaries (1 sentence)
- ✅ Provide detailed descriptions in `<remarks>` sections
- ✅ Include example values for complex types
- ✅ Document all HTTP status codes your endpoint returns
- ✅ Use `[SwaggerOperation]` for operation IDs (useful for client generation)
- ✅ Group related endpoints with Tags

### DON'T:
- ❌ Skip XML comments (they power IntelliSense too!)
- ❌ Leave generic descriptions like "Gets data"
- ❌ Document internal implementation details
- ❌ Expose authentication tokens in examples
- ❌ Include health/metrics endpoints in API docs

## Troubleshooting

### XML Comments Not Appearing

1. Ensure `GenerateDocumentationFile` is `true` in `.csproj`:
   ```xml
   <PropertyGroup>
     <GenerateDocumentationFile>true</GenerateDocumentationFile>
     <NoWarn>$(NoWarn);1591</NoWarn>
   </PropertyGroup>
   ```

2. Rebuild the project:
   ```bash
   dotnet build
   ```

### Swagger UI Not Loading

Check the configuration in `appsettings.{Environment}.json`:

```json
{
  "Swagger": {
    "EnableUI": true
  }
}
```

Verify the environment with:

```bash
echo $ASPNETCORE_ENVIRONMENT
```

### Missing Endpoints in Documentation

Ensure controllers:
- Have `[ApiController]` attribute
- Have `[Route]` attribute
- Use HTTP verb attributes (`[HttpGet]`, `[HttpPost]`, etc.)

## Integration with Nexus

When documenting endpoints that interact with Nexus, include:

1. **Nexus endpoint called** (e.g., `/api/v1/events`)
2. **Data flow** (what data comes from/goes to Nexus)
3. **Error handling** (what happens if Nexus is unavailable)

Example:

```csharp
/// <summary>
/// Retrieves events from Nexus event store.
/// </summary>
/// <remarks>
/// This endpoint queries the Nexus platform event store and returns
/// aggregated events for the specified stream.
///
/// Nexus endpoint: GET /api/v1/events/{streamId}
/// </remarks>
```

## Resources

- [Swashbuckle Documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [OpenAPI Specification](https://swagger.io/specification/)
- [XML Documentation Comments (C#)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [OpenAPI Generator](https://openapi-generator.tech/)

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Template.Api.Infrastructure.Swagger;

/// <summary>
/// Document filter to exclude health check and metrics endpoints from Swagger documentation.
/// These endpoints are for operational monitoring and don't need to be in the public API docs.
/// </summary>
public class ExcludeHealthCheckEndpointsFilter : IDocumentFilter
{
    private static readonly string[] _excludedPaths =
    [
        "/health",
        "/health/live",
        "/health/ready",
        "/metrics"
    ];

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var excludedPath in _excludedPaths)
        {
            swaggerDoc.Paths.Remove(excludedPath);
        }
    }
}

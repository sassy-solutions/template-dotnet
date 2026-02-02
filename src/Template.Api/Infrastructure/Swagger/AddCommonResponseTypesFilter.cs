using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Template.Api.Infrastructure.Swagger;

/// <summary>
/// Adds common response types (500 Internal Server Error) to all operations.
/// </summary>
public class AddCommonResponseTypesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add 500 Internal Server Error if not already present
        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses.Add("500", new OpenApiResponse
            {
                Description = "Internal server error - an unexpected error occurred while processing the request"
            });
        }

        // If there are path parameters, ensure 400 Bad Request is documented
        if (context.ApiDescription.ParameterDescriptions.Any() && !operation.Responses.ContainsKey("400"))
        {
            operation.Responses.Add("400", new OpenApiResponse
            {
                Description = "Bad request - invalid input parameters"
            });
        }
    }
}

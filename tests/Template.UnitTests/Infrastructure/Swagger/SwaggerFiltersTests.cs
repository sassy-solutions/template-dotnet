using FluentAssertions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Template.Api.Infrastructure.Swagger;
using Xunit;

namespace Template.UnitTests.Infrastructure.Swagger;

public class SwaggerFiltersTests
{
    [Fact]
    public void ExcludeHealthCheckEndpointsFilter_RemovesHealthEndpoints()
    {
        // Arrange
        var filter = new ExcludeHealthCheckEndpointsFilter();
        var swaggerDoc = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/health"] = new OpenApiPathItem(),
                ["/health/live"] = new OpenApiPathItem(),
                ["/health/ready"] = new OpenApiPathItem(),
                ["/metrics"] = new OpenApiPathItem(),
                ["/api/hello"] = new OpenApiPathItem()
            }
        };

        // Act
        filter.Apply(swaggerDoc, null!);

        // Assert
        swaggerDoc.Paths.Should().NotContainKey("/health");
        swaggerDoc.Paths.Should().NotContainKey("/health/live");
        swaggerDoc.Paths.Should().NotContainKey("/health/ready");
        swaggerDoc.Paths.Should().NotContainKey("/metrics");
        swaggerDoc.Paths.Should().ContainKey("/api/hello");
    }

    [Fact]
    public void AddCommonResponseTypesFilter_Adds500Response()
    {
        // Arrange
        var filter = new AddCommonResponseTypesFilter();
        var operation = new OpenApiOperation
        {
            Responses = []
        };

        var context = CreateOperationFilterContext();

        // Act
        filter.Apply(operation, context);

        // Assert
        operation.Responses.Should().ContainKey("500");
        operation.Responses["500"].Description.Should().Contain("Internal server error");
    }

    private static OperationFilterContext CreateOperationFilterContext()
    {
        var apiDescription = new Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription();
        var schemaRepository = new SchemaRepository();
        var schemaGenerator = new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(new System.Text.Json.JsonSerializerOptions()));

        return new OperationFilterContext(
            apiDescription,
            schemaGenerator,
            schemaRepository,
            typeof(SwaggerFiltersTests).GetMethod(nameof(CreateOperationFilterContext))!);
    }
}

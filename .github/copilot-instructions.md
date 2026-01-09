# GitHub Copilot Instructions

## Project Context

This is a .NET 9 microservice following hexagonal architecture principles. It is part of the sassy-solutions ecosystem and integrates with the Nexus platform.

## Architecture Guidelines

### Hexagonal Architecture (Ports & Adapters)

When suggesting code, follow this structure:

- **Domain Layer** (`*.Domain/`): Pure business logic, no external dependencies
  - Entities, Value Objects, Domain Events
  - Domain Services (stateless business rules)
  - Repository interfaces (ports)

- **Application Layer** (`*.Application/`): Use cases and orchestration
  - Commands and Queries (CQRS pattern)
  - Application Services
  - DTOs for input/output

- **Infrastructure Layer** (`*.Infrastructure/`): External integrations
  - Repository implementations (adapters)
  - External API clients (Nexus, etc.)
  - Database contexts

- **API Layer** (`*.Api/`): HTTP interface
  - Controllers (thin, delegate to Application layer)
  - Middleware
  - OpenAPI configuration

## Code Style

- Use C# 12+ features (primary constructors, collection expressions)
- Prefer records for DTOs and Value Objects
- Use file-scoped namespaces
- Follow the .editorconfig rules
- Write XML documentation for public APIs

## Patterns to Use

- **Result Pattern**: Return `Result<T>` instead of throwing exceptions for expected failures
- **CQRS**: Separate read and write operations
- **Repository Pattern**: Abstract data access behind interfaces
- **Dependency Injection**: Constructor injection, avoid service locator

## Testing Conventions

- Use xUnit for tests
- Use FluentAssertions for assertions
- Use NSubstitute for mocking
- Name tests: `MethodName_Scenario_ExpectedResult`
- Arrange-Act-Assert pattern

## Nexus Integration

When integrating with Nexus:
- Use the `IHttpClientFactory` pattern
- API calls go through `HttpClient` named "Nexus"
- Handle errors gracefully with proper logging
- Use the configured base URL from `appsettings.json`

## Examples

### Good Controller Example:
```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderCommand command)
    {
        var result = await mediator.Send(command);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.Error);
    }
}
```

### Good Test Example:
```csharp
public class OrderServiceTests
{
    [Fact]
    public async Task CreateOrder_WithValidData_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IOrderRepository>();
        var service = new OrderService(repository);

        // Act
        var result = await service.CreateOrder(new CreateOrderRequest("Test"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
}
```

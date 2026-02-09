Add a new API endpoint to an existing controller.

## Instructions

The user wants to add: $ARGUMENTS

Follow the patterns established in `HelloController.cs`:

1. Add the endpoint method to the appropriate controller
2. Add `[NexusTrack("domain.action")]` for usage tracking
3. Add Swagger annotations (`[SwaggerOperation]`, `[ProducesResponseType]`)
4. Add XML doc comments with `<summary>`, `<remarks>`, `<response>`
5. Add unit test in the corresponding test class
6. Verify build and tests pass

## Endpoint template

```csharp
/// <summary>
/// Description of what this endpoint does.
/// </summary>
[HttpGet("path")]
[NexusTrack("domain.action")]
[SwaggerOperation(
    Summary = "Short summary",
    Description = "Longer description",
    OperationId = "UniqueOperationId",
    Tags = ["ControllerName"]
)]
[ProducesResponseType<ResponseType>(StatusCodes.Status200OK)]
public async Task<IActionResult> MethodNameAsync(CancellationToken cancellationToken)
{
    // Implementation
}
```

## Checklist

- [ ] Endpoint added with correct HTTP verb and route
- [ ] `[NexusTrack]` attribute applied
- [ ] Swagger annotations complete
- [ ] Unit test added
- [ ] Build passes: `dotnet build`
- [ ] Tests pass: `dotnet test`

Add a new feature following the hexagonal architecture pattern.

## Instructions

The user wants to add: $ARGUMENTS

Follow the hexagonal architecture pattern used in this project:

1. **Port** (`Application/Ports/`): Define the interface for any external dependency
2. **DTOs** (`Infrastructure/{Feature}/Dto/`): Create request/response records
3. **Adapter** (`Infrastructure/{Feature}/`): Implement the port (typed HttpClient, database, etc.)
4. **Controller** (`Controllers/`): Thin HTTP layer with `[NexusTrack]` attributes
5. **DI Registration** (`Program.cs`): Wire up the adapter
6. **Tests**: Unit tests for adapter (HttpMessageHandlerStub) + controller (NSubstitute mocks)

## Checklist

- [ ] Port interface in `Application/Ports/`
- [ ] DTO records in `Infrastructure/{Feature}/Dto/`
- [ ] Adapter implementation in `Infrastructure/{Feature}/`
- [ ] Controller with `[NexusTrack]` attributes
- [ ] DI registration in `Program.cs`
- [ ] Unit tests for adapter and controller
- [ ] Swagger annotations (`[SwaggerOperation]`, `[ProducesResponseType]`)
- [ ] Build passes: `dotnet build`
- [ ] Tests pass: `dotnet test`

## Patterns to follow

- Return `NexusResult<T>` from adapter methods (never throw for expected failures)
- Use `[NexusTrack("feature.action")]` on controller endpoints
- Use `sealed record` for DTOs
- Use `sealed class` for adapters
- Add XML doc comments for Swagger

# CLAUDE.md - AI Assistant Context

## Project Overview

This is a .NET 9 microservice that is part of the **sassy-solutions** ecosystem. It integrates with the **Nexus** platform for core functionality.

## Quick Reference

| Aspect | Value |
|--------|-------|
| Framework | .NET 9 |
| Architecture | Hexagonal (Ports & Adapters) |
| API | REST + OpenAPI |
| Container | Docker (multi-stage, Alpine-based) |
| Deployment | Kubernetes via ArgoCD |
| Observability | OpenTelemetry + Serilog |

## Project Structure

```
.
├── src/
│   └── {ServiceName}.Api/          # API layer (Controllers, Middleware)
├── tests/
│   └── {ServiceName}.UnitTests/    # Unit tests (xUnit + FluentAssertions)
├── deploy/
│   ├── argocd/                     # ArgoCD Application definition
│   ├── values.yaml                 # Helm values for deployment
│   └── environments/               # Environment-specific overrides
├── Dockerfile                      # Multi-stage build
└── {ServiceName}.sln               # Solution file
```

## Commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run locally
dotnet run --project src/{ServiceName}.Api

# Docker build
docker build -t {service-name}-api .

# Docker run
docker compose up -d
```

## Architecture Guidelines

### DO:
- Keep controllers thin - delegate to Application services
- Use records for DTOs and Value Objects
- Return `Result<T>` for operations that can fail
- Use dependency injection (constructor injection)
- Write tests for business logic
- Use structured logging with Serilog

### DON'T:
- Put business logic in controllers
- Use static classes for services
- Throw exceptions for expected failures
- Access infrastructure directly from controllers
- Skip input validation

## Nexus Integration

Nexus is the internal platform that provides:
- Event Sourcing capabilities
- Multi-tenant data isolation
- Shared services

### Calling Nexus APIs:
```csharp
public class MyService(IHttpClientFactory clientFactory)
{
    private readonly HttpClient _nexus = clientFactory.CreateClient("Nexus");

    public async Task<SomeData> GetDataAsync()
    {
        var response = await _nexus.GetAsync("/api/v1/some-endpoint");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SomeData>();
    }
}
```

### Nexus API Base URL:
- Local dev: `http://localhost:5100`
- In-cluster: `http://nexus.services.svc.cluster.local`

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `ASPNETCORE_URLS` | Listening URLs | `http://+:8080` |
| `ServiceName` | Service identifier | From config |
| `Nexus__BaseUrl` | Nexus API URL | In-cluster URL |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTel collector | In-cluster URL |

## Health Endpoints

- `/health` - Full health check (all dependencies)
- `/health/live` - Liveness probe (is app running?)
- `/health/ready` - Readiness probe (can handle traffic?)
- `/metrics` - Prometheus metrics

## Deployment

This service is deployed via ArgoCD:

1. **CI** (on push): Build, test, lint
2. **CD** (on main/release): Build image, push to registry, update values.yaml
3. **ArgoCD**: Detects change, deploys with canary strategy

### Environments:
- `dev`: Auto-deploy on main branch push
- `staging`: Manual promotion
- `prod`: Deploy on release tag

### Namespace Pattern:
`{service-name}-{environment}` (e.g., `accelerate-dev`)

## Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~MyTestClass"
```

## Troubleshooting

### Build fails
```bash
dotnet restore
dotnet build --no-incremental
```

### Tests fail
Check if you have the correct .NET SDK:
```bash
dotnet --version  # Should be 9.x
```

### Container won't start
Check health endpoint:
```bash
curl http://localhost:8080/health
```

## Related Resources

- [Infrastructure Repository](https://github.com/sassy-solutions/Infrastructure)
- [Helm Chart](https://github.com/sassy-solutions/Infrastructure/tree/main/helm/charts/dotnet-microservice)
- [ArgoCD Dashboard](https://argocd.sassy.solutions)

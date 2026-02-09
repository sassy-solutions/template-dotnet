# CLAUDE.md - Template Microservice

## Overview

.NET 9 microservice template for the **Sassy Solutions** ecosystem. Uses **hexagonal architecture** with declarative Nexus platform integration via attributes.

## Quick Reference

| Aspect | Value |
|--------|-------|
| Framework | .NET 9, C# 13 |
| Architecture | Hexagonal (Ports & Adapters) |
| Nexus Integration | Attribute-based (`[NexusTrack]`, `[NexusFeature]`, `[NexusAuthorize]`) |
| API | REST + OpenAPI (Swashbuckle) |
| Testing | xUnit + FluentAssertions + NSubstitute |
| Container | Docker multi-stage (Alpine) |
| Deployment | Kubernetes via ArgoCD |
| Observability | OpenTelemetry + Serilog |

## Commands

```bash
dotnet build                                          # Build
dotnet test                                           # Run tests
dotnet test --filter "FullyQualifiedName~MyTest"      # Single test
dotnet test --collect:"XPlat Code Coverage"           # Coverage
dotnet run --project src/Template.Api                 # Run locally
dotnet format --verify-no-changes                     # Lint check
docker build -t template-api .                        # Docker build
```

## Architecture

### Hexagonal (Ports & Adapters) — Single Project

```
src/Template.Api/
├── Controllers/                    # Thin HTTP layer — attributes + delegation
├── Application/
│   ├── Ports/                      # Interfaces (INexusClient, future domain ports)
│   ├── Attributes/                 # Nexus marker attributes ([NexusTrack], etc.)
│   └── Filters/                    # MVC filters that process attributes
├── Domain/
│   └── Models/                     # Business entities, value objects, aggregates
└── Infrastructure/
    ├── Nexus/                      # NexusClient adapter, options, API key handler
    │   └── Dto/                    # Request/response records for Nexus API
    ├── HealthChecks/               # Health check implementations
    └── Swagger/                    # OpenAPI filters and configuration
```

### Rules

- **Controllers are thin**: Business logic goes in Application/Domain, not controllers
- **Ports define contracts**: `Application/Ports/` contains interfaces, never implementations
- **Adapters live in Infrastructure**: Implementations of ports, HTTP clients, database access
- **Records for DTOs**: All request/response types are `record` types
- **Result over exceptions**: Use `NexusResult<T>` (or similar) — never throw for expected failures
- **Constructor injection**: All dependencies via DI

## Nexus Integration

### Declarative (Attributes) — Preferred

Like `[Authorize]` in ASP.NET Identity, Nexus uses attributes to handle SaaS concerns declaratively:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    [HttpPost]
    [NexusTrack("order.created")]           // Automatic usage tracking
    public IActionResult Create(Order order)
    {
        // Pure business logic — Nexus tracking handled by NexusTrackFilter
        return Ok(order);
    }
}
```

**Available attributes:**

| Attribute | Purpose | Status |
|-----------|---------|--------|
| `[NexusTrack("event")]` | Usage tracking (fire-and-forget) | Active |
| `[NexusFeature("flag")]` | Feature flag gating | Stub (future) |
| `[NexusAuthorize(Role="admin")]` | Role/permission authorization | Stub (future) |

`[NexusTrack]` options:
- `TrackOnFailure = true` — also track when the action returns 4xx/5xx

### Direct (Port Injection) — When Needed

For orchestration scenarios where attributes aren't enough:

```csharp
public class AnalyticsController(INexusClient nexus) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardAsync(CancellationToken ct)
    {
        var usage = await nexus.GetUsageAsync(ct);
        return usage.IsSuccess ? Ok(usage.Value) : StatusCode(usage.StatusCode, usage.Error);
    }
}
```

### INexusClient Methods

| Method | Description |
|--------|-------------|
| `TrackAsync(event, metadata?, ct)` | Track usage event |
| `GetUsageAsync(ct)` | Get all usage counters |
| `GetUsageByEventAsync(event, ct)` | Get specific event counter |
| `IsHealthyAsync(ct)` | Health check (no auth) |

All methods return `NexusResult<T>` (never throw for HTTP errors).

### Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `Nexus__BaseUrl` | Nexus API URL | `http://nexus-api.nexus-live.svc.cluster.local` |
| `Nexus__ApiKey` | API key (`nxs_` prefix) | Empty (set via K8s secret) |
| `Nexus__TimeoutSeconds` | HTTP timeout | `10` |

## Adding a New Feature

### 1. Define the port (if external dependency)
```csharp
// Application/Ports/IPaymentGateway.cs
public interface IPaymentGateway
{
    Task<NexusResult<PaymentResult>> ChargeAsync(decimal amount, CancellationToken ct);
}
```

### 2. Create DTOs
```csharp
// Infrastructure/Payments/Dto/PaymentDto.cs
public sealed record PaymentResult(string TransactionId, decimal Amount);
```

### 3. Implement the adapter
```csharp
// Infrastructure/Payments/StripePaymentGateway.cs
public sealed class StripePaymentGateway : IPaymentGateway { ... }
```

### 4. Register in DI (Program.cs)
```csharp
builder.Services.AddHttpClient<IPaymentGateway, StripePaymentGateway>(...);
```

### 5. Add the controller
```csharp
[ApiController]
[Route("api/[controller]")]
public class PaymentController(IPaymentGateway payments) : ControllerBase
{
    [HttpPost]
    [NexusTrack("payment.charged")]
    public async Task<IActionResult> ChargeAsync(ChargeRequest request, CancellationToken ct)
    {
        var result = await payments.ChargeAsync(request.Amount, ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.StatusCode, result.Error);
    }
}
```

### 6. Write tests
- Unit test the adapter with `HttpMessageHandlerStub`
- Unit test the controller with `NSubstitute` mock of the port

## Testing Conventions

- **Framework**: xUnit + FluentAssertions + NSubstitute
- **Integration tests**: `WebApplicationFactory<Program>` for HTTP endpoint tests
- **Unit tests**: Mock ports via NSubstitute, use `HttpMessageHandlerStub` for HTTP adapters
- **Test naming**: `MethodName_Scenario_ExpectedBehavior`
- **Shared helpers**: `TestHelpers/HttpMessageHandlerStub.cs`

## Health Endpoints

| Endpoint | Purpose | K8s Probe |
|----------|---------|-----------|
| `/health` | All registered checks | Monitoring |
| `/health/live` | App is running (no deps) | Liveness |
| `/health/ready` | Dependencies OK | Readiness |

## CI/CD

| Workflow | Trigger | What It Does |
|----------|---------|--------------|
| `ci.yml` | Every push | Build, test, lint |
| `cd.yml` | Push to main | Build image, push to registry, update values.yaml |
| `bootstrap.yml` | Manual dispatch | One-time rename from template names |

CD has a `preflight` job that gracefully skips when `REGISTRY_ENDPOINT` isn't configured (new repos).

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `ASPNETCORE_URLS` | Listening URLs | `http://+:8080` |
| `ServiceName` | Service identifier | From appsettings |
| `Nexus__BaseUrl` | Nexus API URL | In-cluster URL |
| `Nexus__ApiKey` | Nexus API key | K8s secret |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTel collector | In-cluster URL |

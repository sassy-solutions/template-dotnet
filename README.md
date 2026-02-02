# Template Service

> **Note**: This README will be automatically updated when you create a new repository from this template.

A .NET 9 microservice template with full CI/CD, ArgoCD deployment, and Nexus integration.

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://docs.docker.com/get-docker/)
- [kubectl](https://kubernetes.io/docs/tasks/tools/) (for deployment)

### Create a New Service

1. Click "Use this template" on GitHub
2. Name your repository (e.g., `accelerate`, `my-cool-service`)
3. The bootstrap workflow will automatically:
   - Rename all files and namespaces
   - Reset git history
   - Set up ArgoCD deployment

### Local Development

```bash
# Clone your new repository
git clone https://github.com/sassy-solutions/your-service.git
cd your-service

# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Run the API
dotnet run --project src/Template.Api

# Visit http://localhost:5000/swagger
```

### Docker Development

```bash
# Start all services (API + observability)
docker compose up -d

# View logs
docker compose logs -f api

# Stop
docker compose down
```

## Project Structure

```
├── src/
│   └── Template.Api/           # Main API project
│       ├── Controllers/        # API endpoints
│       ├── Program.cs          # Application entry point
│       └── appsettings.json    # Configuration
├── tests/
│   └── Template.UnitTests/     # Unit tests
├── deploy/
│   ├── argocd/                 # ArgoCD application
│   ├── values.yaml             # Helm values
│   └── environments/           # Per-environment config
├── .github/
│   └── workflows/              # CI/CD pipelines
├── Dockerfile                  # Container build
├── docker-compose.yml          # Local development
└── CLAUDE.md                   # AI assistant context
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/` | GET | Service info |
| `/health` | GET | Full health check |
| `/health/live` | GET | Liveness probe |
| `/health/ready` | GET | Readiness probe |
| `/swagger` | GET | OpenAPI documentation |
| `/api/hello` | GET | Hello World example |
| `/api/hello/{name}` | GET | Personalized greeting |

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Production` |
| `ServiceName` | Service identifier | `Template.Api` |
| `Nexus__BaseUrl` | Nexus API URL | Cluster internal |

### Feature Flags (GitHub Variables)

| Variable | Description |
|----------|-------------|
| `SKIP_TESTS` | Set to `true` to skip tests in CI |
| `SKIP_SECURITY_SCAN` | Set to `true` to skip security scanning |

## Deployment

### Automatic (GitOps)

- **Push to `main`**: Deploys to `dev` environment
- **Create release**: Deploys to `prod` environment

### Manual

```bash
# Port-forward to access locally
kubectl port-forward svc/template-api 8080:80 -n template-dev
```

## API Documentation

This service provides comprehensive OpenAPI/Swagger documentation for all endpoints.

### Accessing Documentation

**Swagger UI (Interactive)**:
- **Development**: http://localhost:5000/swagger (enabled by default)
- **Staging**: https://your-service-staging.sassy.solutions/swagger (enabled)
- **Production**: Disabled for security (use JSON spec instead)

**OpenAPI JSON Spec** (available in all environments):
```bash
# Download the OpenAPI specification
curl https://your-service.sassy.solutions/swagger/v1/swagger.json > openapi.json
```

### Generating API Clients

You can auto-generate type-safe clients from the OpenAPI spec:

**TypeScript/JavaScript**:
```bash
# Using openapi-generator-cli
npx @openapitools/openapi-generator-cli generate \
  -i http://localhost:5000/swagger/v1/swagger.json \
  -g typescript-fetch \
  -o ./generated/client

# Using openapi-typescript
npx openapi-typescript http://localhost:5000/swagger/v1/swagger.json -o types.ts
```

**C#**:
```bash
# Using NSwag
dotnet tool install -g NSwag.CodeGeneration.CSharp
nswag openapi2csclient \
  /input:http://localhost:5000/swagger/v1/swagger.json \
  /output:GeneratedClient.cs
```

**Python**:
```bash
pip install openapi-generator-cli
openapi-generator-cli generate \
  -i http://localhost:5000/swagger/v1/swagger.json \
  -g python \
  -o ./generated/client
```

### Configuration

Control Swagger UI availability per environment in `appsettings.{Environment}.json`:

```json
{
  "Swagger": {
    "EnableUI": true  // false in production for security
  }
}
```

### Documentation Standards

When adding new endpoints, ensure:
- XML comments on all public methods and models
- `[ProducesResponseType]` attributes for all response codes
- `[SwaggerOperation]` for rich metadata
- `[SwaggerSchema]` on models with examples
- Parameter descriptions with constraints

Example:
```csharp
/// <summary>
/// Creates a new resource
/// </summary>
/// <param name="request">Resource creation request</param>
/// <response code="201">Resource created successfully</response>
/// <response code="400">Invalid request data</response>
[HttpPost]
[SwaggerOperation(
    Summary = "Create resource",
    Description = "Creates a new resource in the system",
    OperationId = "CreateResource"
)]
[ProducesResponseType<ResourceResponse>(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Create([FromBody] CreateResourceRequest request)
{
    // Implementation
}
```

## Observability

- **Logs**: Structured JSON via Serilog
- **Traces**: OpenTelemetry → Tempo
- **Metrics**: Prometheus endpoint at `/metrics`
- **Dashboards**: Grafana at https://grafana.sassy.solutions

## Contributing

1. Create a feature branch
2. Make your changes
3. Write/update tests
4. Open a pull request

## License

Proprietary - SassySolutions

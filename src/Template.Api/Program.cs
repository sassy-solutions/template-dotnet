using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Nexus.Sdk;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Template.Api.Infrastructure.Auth;
using Template.Api.Infrastructure.Swagger;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Serilog Configuration
// =========================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "Template.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// =========================
// Services Configuration
// =========================
var serviceName = builder.Configuration["ServiceName"] ?? "Template.Api";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.AddOtlpExporter();
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
});

// Health Checks
// The "self" check always returns healthy and verifies the app is responsive
// Nexus health check is registered automatically by AddNexus() below (tagged as "ready")
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Application is running"));
// Add additional health checks here as needed:
// .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })

// API
builder.Services.AddControllers(options =>
{
    // Nexus.Sdk >= 1.0.0-preview.146 exposes AddNexusFilters on MvcOptions
    // (it wires the feature/track/authorize filters), not on FilterCollection.
    options.AddNexusFilters();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = serviceName,
        Version = "v1",
        Description = "A microservice template for the Sassy Solutions ecosystem. " +
                     "Provides RESTful APIs with OpenTelemetry observability, health checks, and integration with Nexus platform.",
        Contact = new()
        {
            Name = "Sassy Solutions",
            Url = new Uri("https://github.com/sassy-solutions")
        },
        License = new()
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Include XML comments
    var xmlFilename = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Enable annotations support
    options.EnableAnnotations();

    // Filter out health check endpoints from Swagger
    options.DocumentFilter<ExcludeHealthCheckEndpointsFilter>();

    // Add common response types
    options.OperationFilter<AddCommonResponseTypesFilter>();
});

// Nexus SDK — one-liner setup for features, tracking, billing, config, health
builder.Services.AddNexus(builder.Configuration);

// Zitadel JWT-bearer auth — validates issuer + per-environment audience + role claims.
// No-op unless Zitadel:Authority is configured (injected at deploy) and not in Development,
// so local runs stay open. Auth is opt-in per endpoint via [Authorize] / [NexusAuthorize].
builder.Services.AddZitadelAuth(builder.Configuration, builder.Environment);

var app = builder.Build();

// =========================
// Middleware Pipeline
// =========================
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

// Swagger JSON spec (available in all environments for API discovery and client generation)
app.UseSwagger();

// Swagger UI (only in Development and Staging for security reasons)
var enableSwaggerUi = builder.Configuration.GetValue<bool>("Swagger:EnableUI", app.Environment.IsDevelopment());
if (enableSwaggerUi)
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{serviceName} v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = $"{serviceName} API Documentation";
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
        options.EnableFilter();
        options.ShowExtensions();
    });

    Log.Information("Swagger UI enabled at /swagger");
}

app.UseRouting();

// Zitadel JWT-bearer auth (no-op when Zitadel is not configured — see AddZitadelAuth).
// Health probes and public routes stay anonymous; controllers opt in with [Authorize].
app.UseAuthentication();
app.UseAuthorization();

// =========================
// Health Check Endpoints
// =========================

// Full health check - all registered checks (for monitoring and alerting)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Liveness probe - only checks if the app is running (no external dependencies)
// Kubernetes uses this to determine if the pod should be restarted
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // No checks - just confirm the app responds
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Readiness probe - checks critical dependencies (tagged as "ready")
// Kubernetes uses this to determine if the pod can receive traffic
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// =========================
// API Endpoints
// =========================
app.MapControllers();

// Root endpoint - useful for quick health checks
app.MapGet("/", () => Results.Ok(new
{
    service = serviceName,
    version = serviceVersion,
    environment = app.Environment.EnvironmentName,
    timestamp = DateTime.UtcNow
}));

// Metrics endpoint placeholder
// Note: OpenTelemetry exports metrics via OTLP to the collector (configured above).
// For direct Prometheus scraping, add: OpenTelemetry.Exporter.Prometheus.AspNetCore
// and configure with: .WithMetrics(m => m.AddPrometheusExporter())
app.MapGet("/metrics", () => Results.Ok(new
{
    message = "Metrics are exported via OpenTelemetry OTLP to the collector",
    endpoint = "otel-collector:4317",
    documentation = "https://opentelemetry.io/docs/languages/net/exporters/"
}));

try
{
    Log.Information("Starting {ServiceName} v{Version}", serviceName, serviceVersion);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible to integration tests
public partial class Program { }

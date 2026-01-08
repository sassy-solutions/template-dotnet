using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

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
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.AddOtlpExporter();
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
});

// Health Checks
builder.Services.AddHealthChecks();
// Add custom health checks here:
// .AddCheck<DatabaseHealthCheck>("database")
// .AddCheck<NexusHealthCheck>("nexus");

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = serviceName,
        Version = "v1",
        Description = "API for Template service"
    });
});

// HTTP Client for Nexus and other services
builder.Services.AddHttpClient("Nexus", client =>
{
    var nexusUrl = builder.Configuration["Nexus:BaseUrl"] ?? "http://nexus.services.svc.cluster.local";
    client.BaseAddress = new Uri(nexusUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

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

// Swagger (available in all environments for API discovery)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{serviceName} v1");
    options.RoutePrefix = "swagger";
});

app.UseRouting();

// =========================
// Health Check Endpoints
// =========================
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Liveness: just check if app is running
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    // Readiness: check all dependencies
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

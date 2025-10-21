using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Features;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Modules;
using Pw.Modules.Api.Features.Auth;
using Pw.Modules.Api.Features.App;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.HttpLogging;
using OpenTelemetry.Exporter;
using Prometheus;
using Prometheus.DotNetRuntime;

var builder = WebApplication.CreateBuilder(args);

// Set global request size limits to 200 MB
const long MaxRequestSizeBytes = 200L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxRequestSizeBytes;
});

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Pw Modules API", Version = "v1" });
});

// Observability: OpenTelemetry Tracing & Metrics
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "Pw.Modules.Api";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]; // e.g., http://otel-collector:4317 or 4318

Console.WriteLine("OTLP endpoint: " + otlpEndpoint);


builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
            o.Filter = httpContext => httpContext.Request.Path != "/metrics"; // skip metrics scraping
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation();

        // Export to OTLP if endpoint is provided
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            t.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        }
    });

// Built-in health checks and HTTP logging
builder.Services.AddHealthChecks();
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.ResponsePropertiesAndHeaders;
    logging.RequestHeaders.Add("User-Agent");
});

var connString = Environment.GetEnvironmentVariable("PW_MODULES_PG")
                 ?? builder.Configuration.GetConnectionString("Postgres");

builder.Services.AddDbContext<ModulesDbContext>(options =>
    options.UseNpgsql(connString));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Increase multipart/form-data body length limit (for file uploads)
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxRequestSizeBytes;
});

var app = builder.Build();

// Apply pending migrations (ensures schema is up to date)
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ModulesDbContext>();
    await db.Database.MigrateAsync();
}

// Enable Swagger UI in all environments and serve at root
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Pw Modules API v1");
    options.RoutePrefix = string.Empty; // serve Swagger UI at application root
});

// Enable HTTP logging and metrics
app.UseHttpLogging();
app.UseCors();
app.UseStaticFiles(); // serve files from wwwroot (for app-updates)

// Prometheus metrics (HTTP + runtime)
app.UseHttpMetrics();
DotNetRuntimeStatsBuilder.Default().StartCollecting();
app.MapMetrics(); // exposes /metrics

// Map endpoints using Vertical Slice Architecture
app.MapAuth();
app.MapModules();
app.MapApp();

// Health endpoints
app.MapHealthChecks("/healthz");

app.Run();
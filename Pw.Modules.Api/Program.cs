using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Features;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Modules;
using Pw.Modules.Api.Features.Auth;
using Pw.Modules.Api.Features.App;
using Microsoft.AspNetCore.HttpLogging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Pw.Modules.Api.Infrastructure.Telegram;

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
    // Default policy kept permissive to avoid breaking existing clients
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

    // Policy for pw-helper.ru
    options.AddPolicy("PwHelper", p =>
        p.WithOrigins(
                "https://pw-helper.ru",
                "http://pw-helper.ru",
                "https://www.pw-helper.ru",
                "http://www.pw-helper.ru"
            )
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()
    );
});

// Increase multipart/form-data body length limit (for file uploads)
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxRequestSizeBytes;
});

// Telegram bot hosted service (starts only if TELEGRAM_BOT_TOKEN is set)
builder.Services.AddHostedService<TelegramBotHostedService>();
// Telegram sender for API initiated messages
builder.Services.AddSingleton<Pw.Modules.Api.Infrastructure.Telegram.ITelegramSender, Pw.Modules.Api.Infrastructure.Telegram.TelegramSender>();

// Custom ActivitySource for the application
var moduleActivitySource = new ActivitySource("Modules");

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

var otel = builder.Services.AddOpenTelemetry();

// Add Metrics for ASP.NET Core and our custom metrics and export via OTLP
otel.WithMetrics(metrics =>
{
    // Metrics provider from OpenTelemetry
    metrics.AddAspNetCoreInstrumentation();
    //Our custom metrics
    metrics.AddMeter(ModuleMetrics.MeterName);
    // Metrics provides by ASP.NET Core in .NET 8
    metrics.AddMeter("Microsoft.AspNetCore.Hosting");
    metrics.AddMeter("Microsoft.AspNetCore.Server.Kestrel");
});

// Add Tracing for ASP.NET Core and our custom ActivitySource and export via OTLP
otel.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddHttpClientInstrumentation();
    tracing.AddSource(moduleActivitySource.Name);
});

// Export OpenTelemetry data via OTLP, using env vars for the configuration
var OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var OtlpService = builder.Configuration["OTEL_SERVICE_NAME"];
if (OtlpEndpoint != null)
{
    Console.WriteLine($"OtlpEndpoint: {OtlpEndpoint}");
    Console.WriteLine($"OtlpService: {OtlpService}");
    otel.UseOtlpExporter();
}

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


// Map endpoints using Vertical Slice Architecture
app.MapAuth();
app.MapModules();
app.MapApp();

// Health endpoints
app.MapHealthChecks("/healthz");

// Register Telegram bot hosted service (conditionally starts if token present)
app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
{
    // No-op hook; background service starts automatically when registered below
});

app.Run();
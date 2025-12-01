using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Features;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Modules;
using Pw.Modules.Api.Features.Auth;
using Pw.Modules.Api.Features.App;
using Pw.Modules.Api.Features.Oidc;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Pw.Modules.Api.Infrastructure.Telegram;

var builder = WebApplication.CreateBuilder(args);

// Set global request size limits to 200 MB
const long MaxRequestSizeBytes = 200L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = MaxRequestSizeBytes; });

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Pw Modules API", Version = "v1" });
});

// Configure Forwarded Headers for reverse proxy support (Nginx/Docker)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Built-in health checks and HTTP logging
builder.Services.AddHealthChecks();
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields =
        HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.ResponsePropertiesAndHeaders;
    logging.RequestHeaders.Add("User-Agent");
});

var connString = Environment.GetEnvironmentVariable("PW_MODULES_PG")
                 ?? builder.Configuration.GetConnectionString("Postgres");

builder.Services.AddDbContext<ModulesDbContext>(options =>
    options.UseNpgsql(connString));

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<ModulesDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token")
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetEndSessionEndpointUris("/connect/logout")
            .SetIntrospectionEndpointUris("/connect/introspect")
            .SetRevocationEndpointUris("/connect/revoke");
            //.SetUserinfoEndpointUris("/connect/userinfo");

        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow();

        options.RegisterScopes(OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Roles);
        options.RegisterScopes(
            "claner",
            "claner:profile",
            "claner:characters:manage",
            "claner:clans:read",
            "claner:clans:join",
            "claner:clans:manage",
            "claner:events:read",
            "claner:events:participate",
            "claner:events:manage"
        );

        options.AddEphemeralEncryptionKey()
            .AddEphemeralSigningKey();

        options.DisableAccessTokenEncryption();

        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .EnableAuthorizationEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .DisableTransportSecurityRequirement();
            //.EnableUserinfoEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthorization();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login-ui"; // Changed to avoid conflict with existing /api/auth/login if mapped there? No, existing is /api/auth/login.
        // I will map the UI login to /login or /auth/login-ui. existing is POST /api/auth/login.
        // Let's use /login for the UI page.
        options.LoginPath = "/login";
    });

builder.Services.AddCors(options =>
{
    // Default policy kept permissive to avoid breaking existing clients
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

    // Policy for pw-hup.ru
    options.AddPolicy("PwHub", p =>
        p.WithOrigins(
                "https://pw-hub.ru",
                "https://www.pw-hub.ru"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
    );
});

// Increase multipart/form-data body length limit (for file uploads)
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = MaxRequestSizeBytes; });

// Telegram bot hosted service (starts only if TELEGRAM_BOT_TOKEN is set)
builder.Services.AddHostedService<TelegramBotHostedService>();
// Telegram sender for API initiated messages
builder.Services
    .AddSingleton<Pw.Modules.Api.Infrastructure.Telegram.ITelegramSender,
        Pw.Modules.Api.Infrastructure.Telegram.TelegramSender>();

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

app.UseForwardedHeaders();

// Apply pending migrations (ensures schema is up to date)
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ModulesDbContext>();
    await db.Database.MigrateAsync();

    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

    var scopes = new[]
    {
        "claner:profile",
        "claner:characters:manage",
        "claner:clans:read",
        "claner:clans:join",
        "claner:clans:manage",
        "claner:events:read",
        "claner:events:participate",
        "claner:events:manage"
    };

    foreach (var scopeName in scopes)
    {
        if (await scopeManager.FindByNameAsync(scopeName) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = scopeName,
                DisplayName = scopeName
            });
        }
    }

    var client = await manager.FindByClientIdAsync("claner");
    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = "claner",
        ClientType = OpenIddictConstants.ClientTypes.Public,
        ClientSecret = null, // ClientSecret is null to make it a Public Client
        DisplayName = "Кланер",
        RedirectUris =
        {
            new Uri("http://localhost:3000/api/auth/callback/claner"),
            new Uri("https://oauth.pstmn.io/v1/callback"),
            new Uri("https://oidcdebugger.com/debug")
        },
        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.EndSession,
            OpenIddictConstants.Permissions.Endpoints.Introspection,
            OpenIddictConstants.Permissions.Endpoints.Revocation,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles
        }
    };

    foreach (var scopeName in scopes)
    {
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);
    }

    if (client is null)
    {
        await manager.CreateAsync(descriptor);
    }
    else
    {
        // Update existing client to match the descriptor (ensures it is Public and has no secret)
        await manager.UpdateAsync(client, descriptor);
    }
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
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles(); // serve files from wwwroot (for app-updates)


// Map endpoints using Vertical Slice Architecture
app.MapAuth();
app.MapModules();
app.MapApp();
OidcEndpoints.Map(app);
LoginEndpoints.Map(app);

// Health endpoints
app.MapHealthChecks("/healthz");

// Register Telegram bot hosted service (conditionally starts if token present)
app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
{
    // No-op hook; background service starts automatically when registered below
});

app.Run();
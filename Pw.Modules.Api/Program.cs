using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Modules;
using Pw.Modules.Api.Features.Auth;
using Pw.Modules.Api.Features.App;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Pw Modules API", Version = "v1" });
});

var connString = Environment.GetEnvironmentVariable("PW_MODULES_PG")
                 ?? builder.Configuration.GetConnectionString("Postgres");

builder.Services.AddDbContext<ModulesDbContext>(options =>
    options.UseNpgsql(connString));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
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

app.UseCors();
app.UseStaticFiles(); // serve files from wwwroot (for app-updates)

// Map endpoints using Vertical Slice Architecture
app.MapAuth();
app.MapModules();
app.MapApp();

// Optional health endpoint
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();
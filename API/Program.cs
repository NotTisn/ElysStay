using Application;
using Infrastructure;
using Infrastructure.Auth;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog early so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// ==========================================
// 1. ADD SERVICES
// ==========================================

// OpenAPI
builder.Services.AddOpenApi();

// Controllers with camelCase JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// CORS - allow local dev clients, adjust origins in production
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Basic health checks
builder.Services.AddHealthChecks();

// Infrastructure: DbContext, Auth services, Keycloak admin client
builder.Services.AddInfrastructure(builder.Configuration);

// Application: MediatR, FluentValidation, pipeline behaviors
builder.Services.AddApplication();

// ================= KEYCLOAK =================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var keycloak = builder.Configuration.GetSection("Keycloak");

        options.Authority = keycloak["Authority"];
        options.Audience = keycloak["ClientId"];
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ==========================================
// AUTO MIGRATE (DEV ONLY)
// ==========================================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

// ==========================================
// PIPELINE
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Global error handling — must be early in pipeline
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Use CORS
app.UseCors("DefaultCorsPolicy");

app.UseAuthentication();
app.UseMiddleware<UserAutoProvisioningMiddleware>();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health endpoint
app.MapHealthChecks("/healthz");

// Public API
app.MapGet("/", () => "Hệ thống Quản lý Trọ API đang chạy ngon lành! 🚀");

// Protected API
app.MapGet("/secure", () => "Bạn đã authenticated!")
   .RequireAuthorization();

try
{
    Log.Information("Starting web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
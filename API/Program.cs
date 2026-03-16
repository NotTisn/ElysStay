using Application;
using Infrastructure;
using Infrastructure.Auth;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Threading.RateLimiting;
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

// Lowercase URLs globally
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

// Controllers with camelCase JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// CORS — restrict origins per environment; adjust AllowedOrigins in appsettings for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000", "http://localhost:3001"];

        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .WithHeaders("Content-Type", "Authorization", "Accept", "X-Requested-With")
              .AllowCredentials();
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
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

// ================= RATE LIMITING =================
// AUTH-04: Fixed window rate limiting per IP address.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        Log.Warning("Rate limit exceeded for {IP}", GetClientIp(context.HttpContext));
        await Task.CompletedTask;
    };

    // "sensitive" policy: password changes, user creation — 5 req/min per IP
    options.AddPolicy("sensitive", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientIp(context),
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0
            }));

    // Global limiter: 100 req/min per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientIp(context),
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 100,
                QueueLimit = 0
            }));
});

// F25: Helper to extract real client IP behind reverse proxies
static string GetClientIp(HttpContext context)
{
    // Check X-Forwarded-For first (set by reverse proxies)
    var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrEmpty(forwarded))
    {
        // Take the first IP (original client)
        return forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
    }
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

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

// Global error handling — must be first in pipeline
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Security headers (F24: added Content-Security-Policy)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("DefaultCorsPolicy");

// Rate limiting — before auth so abusive traffic is shed early
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<UserAutoProvisioningMiddleware>();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health endpoint
app.MapHealthChecks("/healthz");

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
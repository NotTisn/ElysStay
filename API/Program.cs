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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;

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

// Swagger UI (Swashbuckle)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ElysStay API",
        Version = "v1",
        Description = "Property Management API for ElysStay"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

   c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
{
    {
        new OpenApiSecuritySchemeReference("Bearer"),
        []
    }
});
});

// Lowercase URLs globally
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

// Controllers with camelCase JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// CORS — origins configured per environment in appsettings (empty = block all cross-origin)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? [];

        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .WithHeaders("Content-Type", "Authorization", "Accept", "X-Requested-With")
              .AllowCredentials();
    });
});

// Basic health checks
builder.Services.AddHealthChecks()
    .AddCheck<Infrastructure.BackgroundJobs.BackgroundJobHealthCheck>("background_jobs");

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
        // Accept tokens issued by the frontend (elysstay-fe) as well as
        // Keycloak's default "account" audience and the backend's own client-id.
        options.Audience = keycloak["ClientId"];

        // Allow override for reverse-proxy deployments (internal HTTP to Keycloak)
        var requireHttps = keycloak["RequireHttpsMetadata"];
        options.RequireHttpsMetadata = requireHttps != null
            ? bool.Parse(requireHttps)
            : !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudiences = keycloak.GetSection("ValidAudiences").Get<string[]>()
                ?? [keycloak["ClientId"]!, "account"],
            ValidateIssuer = true,
            ValidateLifetime = true
        };

        // Reverse-proxy support: token issuer (public URL) may differ from Authority (internal URL)
        var validIssuer = keycloak["ValidIssuer"];
        if (!string.IsNullOrEmpty(validIssuer))
            options.TokenValidationParameters.ValidIssuer = validIssuer;
    });

builder.Services.AddAuthorization();

// ================= RATE LIMITING =================
// AUTH-04: Fixed window rate limiting per IP address.
var rateLimitMeter = new System.Diagnostics.Metrics.Meter("ElysStay.RateLimiting");
var rateLimitRejectedCounter = rateLimitMeter.CreateCounter<int>("rate_limit_rejected_requests", description: "Number of rejected requests due to rate limiting");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        var ip = GetClientIp(context.HttpContext);
        Log.Warning("Rate limit exceeded for {IP}", ip);
        rateLimitRejectedCounter.Add(1, new KeyValuePair<string, object?>("client_ip", ip));
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
    // Use the framework-validated RemoteIpAddress (safe when using ForwardedHeaders middleware)
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // In production, API is only reachable through Caddy in Docker network.
    // Trust the standard Docker bridge subnet and common private networks.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(
        System.Net.IPAddress.Parse("172.16.0.0"), 12));  // Docker default bridge
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(
        System.Net.IPAddress.Parse("10.0.0.0"), 8));     // Docker custom networks
    options.ForwardLimit = 1; // Only trust one proxy hop
});

var app = builder.Build();

// ==========================================
// AUTO MIGRATE + DEV SEED
// ==========================================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Retry migration up to 5 times (2s backoff) — covers Docker container startup race
    const int maxRetries = 5;
    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            dbContext.Database.Migrate();
            break;
        }
        catch (Npgsql.NpgsqlException ex) when (attempt < maxRetries)
        {
            startupLogger.LogWarning(ex,
                "Database connection failed (attempt {Attempt}/{Max}). Retrying in 2s...",
                attempt, maxRetries);
            await Task.Delay(2000);
        }
    }

    await Infrastructure.Persistence.DevDataSeeder.SeedAsync(dbContext, startupLogger);
}

// ==========================================
// PIPELINE
// ==========================================

// Global error handling — must be first in pipeline
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Forward proxy headers so rate limiter uses real client IP
app.UseForwardedHeaders();

// Security headers (F24: added Content-Security-Policy)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=()";

    if (context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "img-src 'self' data:; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self' 'unsafe-inline';";
    }
    else
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; frame-ancestors 'none'";
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ElysStay API v1");
        c.RoutePrefix = "swagger";
    });
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
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

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

// PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

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

// Use CORS
app.UseCors("DefaultCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

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
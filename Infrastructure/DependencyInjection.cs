using Application.Common.Interfaces;
using Infrastructure.Auth;
using Infrastructure.BackgroundJobs;
using Infrastructure.Configuration;
using Infrastructure.Email;
using Infrastructure.FileUpload;
using Infrastructure.Ocr;
using Infrastructure.Pdf;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>
/// Registers all Infrastructure services into the DI container.
/// Called from Program.cs bootup.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Persistence ──
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                   // Soft-delete query filters on User/Building/Room/Expense intentionally
                   // affect required navigations — suppress the resulting startup warnings.
                   .ConfigureWarnings(w => w.Ignore(
                       Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteraction)));

        // Expose the DbContext via the IApplicationDbContext interface
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ── Auth ──
        services.AddHttpContextAccessor();

        // Claims transformation: Keycloak realm_access → ClaimTypes.Role
        services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformation>();

        // Current user (scoped; populated by UserAutoProvisioningMiddleware)
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());

        // Building-scope authorization
        services.AddScoped<IBuildingScopeService, BuildingScopeService>();

        // Building defaults (typed config for default service prices)
        services.AddSingleton<IBuildingDefaultsProvider>(sp =>
            new BuildingDefaultsProvider(configuration));

        // Keycloak Admin API client
        var keycloakSection = configuration.GetSection("Keycloak");
        var keycloakOptions = new KeycloakAdminOptions
        {
            BaseUrl = keycloakSection["Authority"]?.Replace("/realms/elysstay", "") ?? "http://localhost:8080",
            Realm = "elysstay",
            ClientId = keycloakSection["ClientId"] ?? "elysstay-be",
            ClientSecret = keycloakSection["ClientSecret"] ?? string.Empty
        };
        services.AddSingleton(keycloakOptions);

        services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>(client =>
        {
            client.BaseAddress = new Uri(keycloakOptions.BaseUrl.TrimEnd('/') + "/");
        });

        // Email (Resend free tier — no-op if Email:ApiKey is not set)
        services.AddHttpClient<IEmailService, ResendEmailService>();

        // File uploads (Cloudinary free tier — no-op if Cloudinary section is not set)
        services.AddSingleton<IFileUploadService, CloudinaryUploadService>();

        // Invoice PDF generation (QuestPDF Community)
        services.AddSingleton<IInvoicePdfService, InvoicePdfService>();

        // OCR (FPT.AI free tier — no-op if FptAi:ApiKey is not set)
        services.AddHttpClient<IOcrService, FptAiOcrService>();

        // Background jobs (BG-01, BG-02, BG-03)
        services.AddHostedService<ReservationExpiryBackgroundService>();
        services.AddHostedService<InvoiceOverdueBackgroundService>();
        services.AddHostedService<ContractExpiryAlertBackgroundService>();

        return services;
    }
}

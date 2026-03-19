using System.Diagnostics;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// BG-03: Daily at 08:00 UTC, alerts owners and tenants for active contracts
/// with EndDate <= today + 30 days.
/// </summary>
public class ContractExpiryAlertBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ContractExpiryAlertBackgroundService> _logger;

    public ContractExpiryAlertBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ContractExpiryAlertBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BG-03 ContractExpiryAlertJob started — runs daily at 08:00 UTC");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Contract expiry alert background job failed");
            }

            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(8);
            var delay = nextRun - now;
            if (delay < TimeSpan.FromMinutes(1))
                delay = TimeSpan.FromMinutes(1);

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var threshold = today.AddDays(30);

        var contracts = await db.Contracts
            .Include(c => c.Room)
                .ThenInclude(r => r!.Building)
            .Include(c => c.TenantUser)
            .Where(c => c.Status == ContractStatus.Active && c.EndDate <= threshold)
            .ToListAsync(ct);

        if (contracts.Count == 0)
        {
            _logger.LogDebug("BG-03 ContractExpiryAlertJob: no expiring contracts found ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
            return;
        }

        var now = DateTime.UtcNow;

        // Dedup: Only send one alert per contract per 7-day window.
        // Without this, tenants/owners get 30 duplicate alerts over the 30-day window.
        var contractIds = contracts.Select(c => c.Id).ToList();
        var recentCutoff = now.AddDays(-7);
        var recentlyNotified = await db.Notifications
            .Where(n => n.Type == "CONTRACT_EXPIRY_ALERT"
                        && n.ReferenceId.HasValue
                        && contractIds.Contains(n.ReferenceId.Value)
                        && n.CreatedAt >= recentCutoff)
            .Select(n => new { n.UserId, n.ReferenceId })
            .ToListAsync(ct);
        var notifiedSet = recentlyNotified
            .Select(n => (n.UserId, n.ReferenceId!.Value))
            .ToHashSet();

        foreach (var contract in contracts)
        {
            if (!notifiedSet.Contains((contract.TenantUserId, contract.Id)))
            {
                db.Notifications.Add(new Notification
                {
                    UserId = contract.TenantUserId,
                    Title = "Cảnh báo hợp đồng sắp hết hạn",
                    Message = $"Hợp đồng phòng {contract.Room!.RoomNumber} sẽ hết hạn vào {contract.EndDate:yyyy-MM-dd}.",
                    Type = "CONTRACT_EXPIRY_ALERT",
                    ReferenceId = contract.Id,
                    CreatedAt = now
                });
            }

            var ownerId = contract.Room!.Building!.OwnerId;
            if (!notifiedSet.Contains((ownerId, contract.Id)))
            {
                db.Notifications.Add(new Notification
                {
                    UserId = ownerId,
                    Title = "Cảnh báo hợp đồng sắp hết hạn",
                    Message = $"Hợp đồng phòng {contract.Room.RoomNumber} của khách {contract.TenantUser!.FullName} sẽ hết hạn vào {contract.EndDate:yyyy-MM-dd}.",
                    Type = "CONTRACT_EXPIRY_ALERT",
                    ReferenceId = contract.Id,
                    CreatedAt = now
                });
            }
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("BG-03 ContractExpiryAlertJob: alerted for {Count} expiring contracts in {ElapsedMs}ms", contracts.Count, sw.ElapsedMilliseconds);
    }
}

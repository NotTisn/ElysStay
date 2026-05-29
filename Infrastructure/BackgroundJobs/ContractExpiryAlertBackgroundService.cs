using System.Diagnostics;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Application.Common.Email;
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
    private readonly BackgroundJobHealthCheck _healthCheck;

    public ContractExpiryAlertBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ContractExpiryAlertBackgroundService> logger,
        BackgroundJobHealthCheck healthCheck)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthCheck = healthCheck;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BG-03 ContractExpiryAlertJob started — runs daily at 08:00 UTC");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var retries = 3;
                while (retries > 0)
                {
                    try
                    {
                        await RunOnceAsync(stoppingToken);
                        _healthCheck.ReportJobRun("ContractExpiryAlert");
                        break;
                    }
                    catch (Exception ex)
                    {
                        retries--;
                        if (retries == 0)
                        {
                            _logger.LogError(ex, "ContractExpiryAlertJob failed after retries.");
                            _healthCheck.ReportJobError("ContractExpiryAlert", ex);
                        }
                        else
                        {
                            _logger.LogWarning(ex, "ContractExpiryAlertJob error, retrying... ({RetriesLeft} retries left)", retries);
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
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
            .Where(c => c.Status == ContractStatus.Active && c.EndDate <= threshold)
            .Select(c => new { c.Id, c.TenantUserId, OwnerId = c.Room!.Building!.OwnerId })
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

        var processed = 0;

        foreach (var contract in contracts)
        {
            if (notifiedSet.Contains((contract.TenantUserId, contract.Id))
                && notifiedSet.Contains((contract.OwnerId, contract.Id)))
            {
                continue;
            }

            try
            {
                await ProcessContractAsync(contract.Id, now, notifiedSet, ct);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BG-03 ContractExpiryAlertJob: failed to process contract {ContractId}", contract.Id);
            }
        }

        _logger.LogInformation("BG-03 ContractExpiryAlertJob: alerted for {Processed}/{Total} expiring contracts in {ElapsedMs}ms", processed, contracts.Count, sw.ElapsedMilliseconds);
    }

    private async Task ProcessContractAsync(Guid contractId, DateTime now, HashSet<(Guid UserId, Guid ContractId)> notifiedSet, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var contract = await db.Contracts
            .Include(c => c.Room)
                .ThenInclude(r => r!.Building!)
                    .ThenInclude(b => b.Owner)
            .Include(c => c.TenantUser)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new InvalidOperationException($"Contract {contractId} was not found during expiry alert processing.");

        var tenantNotified = notifiedSet.Contains((contract.TenantUserId, contract.Id));
        if (!tenantNotified)
        {
            db.Notifications.Add(new Notification
            {
                UserId = contract.TenantUserId,
                Title = "Cảnh báo hợp đồng sắp hết hạn",
                Message = $"Hợp đồng phòng {contract.Room!.RoomNumber} sẽ hết hạn vào {contract.EndDate:yyyy-MM-dd}.",
                Type = Domain.Constants.NotificationTypes.ContractExpiryAlert,
                ReferenceId = contract.Id,
                CreatedAt = now
            });
        }

        var ownerId = contract.Room!.Building!.OwnerId;
        var ownerNotified = notifiedSet.Contains((ownerId, contract.Id));
        if (!ownerNotified)
        {
            db.Notifications.Add(new Notification
            {
                UserId = ownerId,
                Title = "Cảnh báo hợp đồng sắp hết hạn",
                Message = $"Hợp đồng phòng {contract.Room.RoomNumber} của khách {contract.TenantUser!.FullName} sẽ hết hạn vào {contract.EndDate:yyyy-MM-dd}.",
                Type = Domain.Constants.NotificationTypes.ContractExpiryAlert,
                ReferenceId = contract.Id,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "BG-03 ContractExpiryAlertJob: alerted contract {ContractId} (ends {EndDate:yyyy-MM-dd}, room {RoomNumber}) — tenant notified: {TenantNotified}, owner notified: {OwnerNotified}",
            contract.Id, contract.EndDate, contract.Room?.RoomNumber, !tenantNotified, !ownerNotified);

        var tenant = contract.TenantUser;
        var room = contract.Room;
        var building = room?.Building;
        if (tenant != null && room != null && building != null)
        {
            if (!tenantNotified)
            {
                var (s1, h1) = EmailTemplates.ContractExpiryTenant(
                    tenant.FullName, room.RoomNumber, building.Name, contract.EndDate);
                await emailService.TrySendAsync(tenant.Email, tenant.FullName, s1, h1, ct);
                notifiedSet.Add((tenant.Id, contract.Id));
            }

            if (building.Owner != null && !ownerNotified)
            {
                var (s2, h2) = EmailTemplates.ContractExpiryOwner(
                    building.Owner.FullName, tenant.FullName,
                    room.RoomNumber, building.Name, contract.EndDate);
                await emailService.TrySendAsync(building.Owner.Email, building.Owner.FullName, s2, h2, ct);
                notifiedSet.Add((building.OwnerId, contract.Id));
            }
        }
    }
}

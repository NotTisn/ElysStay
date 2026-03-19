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
/// BG-01: Every hour, expires pending/confirmed reservations past ExpiresAt
/// and releases room status BOOKED -> AVAILABLE.
/// </summary>
public class ReservationExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationExpiryBackgroundService> _logger;

    public ReservationExpiryBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ReservationExpiryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BG-01 ReservationExpiryJob started — runs every hour");

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
                _logger.LogError(ex, "Reservation expiry background job failed");
            }

            var now = DateTime.UtcNow;
            var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
            var delay = nextHour - now;
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
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var expiredReservations = await db.RoomReservations
            .Include(r => r.Room).ThenInclude(r => r!.Building!)
            .Include(r => r.TenantUser)
            .Where(r => (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                        && r.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expiredReservations.Count == 0)
        {
            _logger.LogDebug("BG-01 ReservationExpiryJob: no expired reservations found ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
            return;
        }

        foreach (var reservation in expiredReservations)
        {
            reservation.Status = ReservationStatus.Expired;
            reservation.UpdatedAt = now;

            if (reservation.Room is not null && reservation.Room.Status == RoomStatus.Booked)
            {
                reservation.Room.Status = RoomStatus.Available;
                reservation.Room.UpdatedAt = now;
            }

            // Notify tenant that their reservation has expired
            db.Notifications.Add(new Notification
            {
                UserId = reservation.TenantUserId,
                Title = "Đặt phòng đã hết hạn",
                Message = $"Đặt phòng {reservation.Room?.RoomNumber ?? "N/A"} đã hết hạn và bị hủy tự động.",
                Type = "RESERVATION_EXPIRED",
                ReferenceId = reservation.Id,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        // Best-effort emails to tenants (after successful save)
        foreach (var reservation in expiredReservations)
        {
            var tenant = reservation.TenantUser;
            var room = reservation.Room;
            if (tenant != null && room != null)
            {
                var (subject, html) = EmailTemplates.ReservationExpired(
                    tenant.FullName, room.RoomNumber, room.Building?.Name ?? "N/A");
                await emailService.TrySendAsync(tenant.Email, tenant.FullName, subject, html, ct);
            }
        }

        _logger.LogInformation("BG-01 ReservationExpiryJob: expired {Count} reservations in {ElapsedMs}ms", expiredReservations.Count, sw.ElapsedMilliseconds);
    }
}

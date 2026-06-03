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
    private readonly BackgroundJobHealthCheck _healthCheck;

    public ReservationExpiryBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ReservationExpiryBackgroundService> logger,
        BackgroundJobHealthCheck healthCheck)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthCheck = healthCheck;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BG-01 ReservationExpiryJob started — runs every hour");

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
                        _healthCheck.ReportJobRun("ReservationExpiry");
                        break;
                    }
                    catch (Exception ex)
                    {
                        retries--;
                        if (retries == 0)
                        {
                            _logger.LogError(ex, "ReservationExpiryJob failed after retries.");
                            _healthCheck.ReportJobError("ReservationExpiry", ex);
                        }
                        else
                        {
                            _logger.LogWarning(ex, "ReservationExpiryJob error, retrying... ({RetriesLeft} retries left)", retries);
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

        var now = DateTime.UtcNow;
        var expiredReservationIds = await db.RoomReservations
            .Where(r => (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                        && r.ExpiresAt <= now)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (expiredReservationIds.Count == 0)
        {
            _logger.LogDebug("BG-01 ReservationExpiryJob: no expired reservations found ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
            return;
        }

        var processed = 0;
        foreach (var reservationId in expiredReservationIds)
        {
            try
            {
                await ProcessReservationAsync(reservationId, now, ct);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BG-01 ReservationExpiryJob: failed to expire reservation {ReservationId}", reservationId);
            }
        }

        _logger.LogInformation("BG-01 ReservationExpiryJob: expired {Processed}/{Total} reservations in {ElapsedMs}ms", processed, expiredReservationIds.Count, sw.ElapsedMilliseconds);
    }

    private async Task ProcessReservationAsync(Guid reservationId, DateTime now, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var reservation = await db.RoomReservations
            .Include(r => r.Room).ThenInclude(r => r!.Building!)
            .Include(r => r.TenantUser)
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct)
            ?? throw new InvalidOperationException($"Reservation {reservationId} was not found during expiry processing.");

        var wasConfirmed = reservation.Status == ReservationStatus.Confirmed;

        reservation.Status = ReservationStatus.Expired;
        reservation.UpdatedAt = now;

        if (wasConfirmed && reservation.DepositAmount > 0)
        {
            reservation.RefundAmount = 0;
            reservation.RefundNote = "Tiền cọc bị mất do hết hạn đặt phòng.";

            db.Payments.Add(new Payment
            {
                ReservationId = reservation.Id,
                Type = PaymentType.DepositIn,
                Amount = reservation.DepositAmount,
                Note = "Tiền cọc nhận từ đặt phòng (hết hạn — tịch thu)",
                RecordedBy = reservation.TenantUserId,
                PaidAt = reservation.CreatedAt
            });
        }

        if (reservation.Room is not null && reservation.Room.Status == RoomStatus.Reserved)
        {
            reservation.Room.Status = RoomStatus.Available;
            reservation.Room.UpdatedAt = now;
        }

        db.Notifications.Add(new Notification
        {
            UserId = reservation.TenantUserId,
            Title = "Đặt phòng đã hết hạn",
            Message = $"Đặt phòng {reservation.Room?.RoomNumber ?? "N/A"} đã hết hạn và bị hủy tự động.",
            Type = Domain.Constants.NotificationTypes.ReservationExpired,
            ReferenceId = reservation.Id,
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);

        var tenant = reservation.TenantUser;
        var room = reservation.Room;
        if (tenant != null && room != null)
        {
            var (subject, html) = EmailTemplates.ReservationExpired(
                tenant.FullName, room.RoomNumber, room.Building?.Name ?? "N/A");
            await emailService.TrySendAsync(tenant.Email, tenant.FullName, subject, html, ct);
        }
    }
}

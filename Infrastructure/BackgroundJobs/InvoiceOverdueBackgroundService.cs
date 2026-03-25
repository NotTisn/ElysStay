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
/// BG-02: Daily at 00:00 UTC, marks SENT invoices past DueDate as OVERDUE
/// and notifies tenants.
/// </summary>
public class InvoiceOverdueBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InvoiceOverdueBackgroundService> _logger;

    public InvoiceOverdueBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<InvoiceOverdueBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BG-02 InvoiceOverdueJob started — runs daily at midnight UTC");

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
                _logger.LogError(ex, "Invoice overdue background job failed");
            }

            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1);
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

        // Include PartiallyPaid — a tenant who pays $1 on a $1000 invoice
        // should still transition to Overdue after the due date passes.
        var overdueInvoiceIds = await db.Invoices
            .Where(i => (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid)
                        && i.DueDate < today)
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (overdueInvoiceIds.Count == 0)
        {
            _logger.LogDebug("BG-02 InvoiceOverdueJob: no overdue invoices found ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
            return;
        }

        var now = DateTime.UtcNow;

        var processed = 0;

        foreach (var invoiceId in overdueInvoiceIds)
        {
            try
            {
                await ProcessInvoiceAsync(invoiceId, now, ct);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BG-02 InvoiceOverdueJob: failed to mark invoice {InvoiceId} overdue", invoiceId);
            }
        }

        _logger.LogInformation("BG-02 InvoiceOverdueJob: marked {Processed}/{Total} invoices overdue in {ElapsedMs}ms", processed, overdueInvoiceIds.Count, sw.ElapsedMilliseconds);
    }

    private async Task ProcessInvoiceAsync(Guid invoiceId, DateTime now, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var invoice = await db.Invoices
            .Include(i => i.Contract).ThenInclude(c => c!.Room!).ThenInclude(r => r.Building!)
            .Include(i => i.Contract).ThenInclude(c => c!.TenantUser!)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} was not found during overdue processing.");

        invoice.Status = InvoiceStatus.Overdue;
        invoice.UpdatedAt = now;

        _logger.LogInformation(
            "BG-02 InvoiceOverdueJob: marking invoice {InvoiceId} (month {Month}/{Year}, total {Total:N0}) as Overdue for tenant {TenantId}",
            invoice.Id, invoice.BillingMonth, invoice.BillingYear, invoice.TotalAmount, invoice.Contract?.TenantUserId);

        db.Notifications.Add(new Notification
        {
            UserId = invoice.Contract!.TenantUserId,
            Title = "Hóa đơn quá hạn",
            Message = $"Hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} đã quá hạn thanh toán.",
            Type = Domain.Constants.NotificationTypes.InvoiceOverdue,
            ReferenceId = invoice.Id,
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);

        var tenant = invoice.Contract.TenantUser;
        var room = invoice.Contract.Room;
        if (tenant != null && room != null)
        {
            var (subject, html) = EmailTemplates.InvoiceOverdue(
                tenant.FullName, room.RoomNumber, room.Building!.Name,
                invoice.BillingMonth, invoice.BillingYear, invoice.TotalAmount);
            await emailService.TrySendAsync(tenant.Email, tenant.FullName, subject, html, ct);
        }
    }
}

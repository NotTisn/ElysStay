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
        var overdueInvoices = await db.Invoices
            .Include(i => i.Contract)
            .Where(i => (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid)
                        && i.DueDate < today)
            .ToListAsync(ct);

        if (overdueInvoices.Count == 0)
        {
            _logger.LogDebug("BG-02 InvoiceOverdueJob: no overdue invoices found ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
            return;
        }

        var now = DateTime.UtcNow;

        foreach (var invoice in overdueInvoices)
        {
            invoice.Status = InvoiceStatus.Overdue;
            invoice.UpdatedAt = now;

            db.Notifications.Add(new Notification
            {
                UserId = invoice.Contract!.TenantUserId,
                Title = "Hóa đơn quá hạn",
                Message = $"Hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} đã quá hạn thanh toán.",
                Type = "INVOICE_OVERDUE",
                ReferenceId = invoice.Id,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("BG-02 InvoiceOverdueJob: marked {Count} invoices overdue in {ElapsedMs}ms", overdueInvoices.Count, sw.ElapsedMilliseconds);
    }
}

using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Queries;

/// <summary>
/// GET /invoices/{id}/export — Generate and return a PDF for the given invoice.
/// Auth: ALL authenticated users (building-scope enforced by invoice ownership).
/// </summary>
public record ExportInvoicePdfQuery(Guid InvoiceId) : IRequest<InvoicePdfResult>;

public record InvoicePdfResult(byte[] PdfBytes, string FileName);

public class ExportInvoicePdfQueryHandler : IRequestHandler<ExportInvoicePdfQuery, InvoicePdfResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IInvoicePdfService _pdfService;

    public ExportInvoicePdfQueryHandler(IApplicationDbContext db, IInvoicePdfService pdfService)
    {
        _db = db;
        _pdfService = pdfService;
    }

    public async Task<InvoicePdfResult> Handle(ExportInvoicePdfQuery request, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceDetails)
                .ThenInclude(d => d.Service)
            .Include(i => i.Contract)
                .ThenInclude(c => c!.Room)
                    .ThenInclude(r => r!.Building)
                        .ThenInclude(b => b!.Owner)
            .Include(i => i.Contract)
                .ThenInclude(c => c!.TenantUser)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct)
            ?? throw new NotFoundException("Hóa đơn", request.InvoiceId);

        var contract = invoice.Contract!;
        var room = contract.Room!;
        var building = room.Building!;
        var tenant = contract.TenantUser!;

        var paidAmount = invoice.Payments
            .Sum(p => p.Amount);

        var details = invoice.InvoiceDetails
            .Select(d => new InvoiceDetailLine
            {
                ServiceName = d.Service?.Name ?? d.Description,
                Unit = d.Service?.Unit ?? "",
                OldReading = d.PreviousReading,
                NewReading = d.CurrentReading,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                Amount = d.Amount
            }).ToList();

        var data = new InvoicePdfData
        {
            BuildingName = building.Name,
            BuildingAddress = building.Address,
            OwnerName = building.Owner?.FullName ?? "—",
            RoomNumber = room.RoomNumber,
            TenantName = tenant.FullName,
            BillingMonth = invoice.BillingMonth,
            BillingYear = invoice.BillingYear,
            RentAmount = invoice.RentAmount,
            ServiceAmount = invoice.ServiceAmount,
            PenaltyAmount = invoice.PenaltyAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = paidAmount,
            DueDate = invoice.DueDate,
            Status = invoice.Status.ToString(),
            CreatedAt = invoice.CreatedAt,
            Details = details,
            Note = invoice.Note
        };

        var pdfBytes = _pdfService.Generate(data);
        var fileName = $"HoaDon_{building.Name}_{room.RoomNumber}_{invoice.BillingMonth:D2}-{invoice.BillingYear}.pdf";

        return new InvoicePdfResult(pdfBytes, fileName);
    }
}

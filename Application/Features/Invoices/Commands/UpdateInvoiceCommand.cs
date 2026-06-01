using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Invoices.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Commands;

/// <summary>
/// Edit penalty/discount/note on an invoice.
/// Allowed when: Draft, Sent, Overdue, PartiallyPaid.
/// Blocked when: Paid (final), Void (cancelled).
/// Status transitions are handled by dedicated commands:
///   Draft → Sent  : SendInvoiceCommand
///   → Void        : VoidInvoiceCommand (Owner only)
///   → PartiallyPaid / Paid : automated on payment recording
///   → Overdue     : automated by background job
/// </summary>
public record UpdateInvoiceCommand : IRequest<InvoiceDto>
{
    public Guid Id { get; init; }
    public decimal? PenaltyAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public string? Note { get; init; }
}

public class UpdateInvoiceCommandHandler : IRequestHandler<UpdateInvoiceCommand, InvoiceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public UpdateInvoiceCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<InvoiceDto> Handle(UpdateInvoiceCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var invoice = await _db.Invoices
            .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(i => i.Contract!).ThenInclude(c => c.ContractTenants).ThenInclude(ct => ct.Tenant!)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn", request.Id);

        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Void)
            throw new ConflictException("Không thể chỉnh sửa hóa đơn đã thanh toán đầy đủ hoặc đã hủy.");

        await _buildingScope.AuthorizeAsync(invoice.Contract!.Room!.BuildingId, cancellationToken);

        var paidAmount = invoice.Payments
            .Where(p => p.Type == PaymentType.RentPayment)
            .Sum(p => p.Amount);

        var financialChanged = false;

        if (request.PenaltyAmount.HasValue)
        {
            if (request.PenaltyAmount.Value < 0)
                throw new BadRequestException("Tiền phạt không thể âm.");
            invoice.PenaltyAmount = request.PenaltyAmount.Value;
            financialChanged = true;
        }

        if (request.DiscountAmount.HasValue)
        {
            if (request.DiscountAmount.Value < 0)
                throw new BadRequestException("Tiền giảm giá không thể âm.");
            invoice.DiscountAmount = request.DiscountAmount.Value;
            financialChanged = true;
        }

        if (request.Note is not null)
            invoice.Note = request.Note;

        if (financialChanged)
        {
            var newTotalAmount = invoice.RentAmount + invoice.ServiceAmount + invoice.PenaltyAmount - invoice.DiscountAmount;

            if (newTotalAmount < 0)
                throw new BadRequestException("Giảm giá vượt quá tổng hóa đơn. Tổng tiền không thể âm.");

            if (newTotalAmount < paidAmount)
                throw new BadRequestException(
                    $"Tổng tiền mới ({newTotalAmount:N0}) không thể nhỏ hơn số tiền đã thanh toán ({paidAmount:N0}).");

            invoice.TotalAmount = newTotalAmount;
        }

        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return InvoiceDtoMapper.MapToDto(invoice, invoice.Contract!, invoice.Contract!.Room!.Building!, paidAmount);
    }
}

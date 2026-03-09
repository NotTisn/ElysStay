using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Invoices.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Commands;

/// <summary>
/// Edit penalty/discount on an invoice.
/// Only when status = DRAFT or SENT.
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
            .Include(i => i.Contract!).ThenInclude(c => c.TenantUser!)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Invoice", request.Id);

        if (invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Sent)
            throw new ConflictException("Only DRAFT or SENT invoices can be edited.");

        await _buildingScope.AuthorizeAsync(invoice.Contract!.Room!.BuildingId, cancellationToken);

        var changed = false;

        if (request.PenaltyAmount.HasValue)
        {
            invoice.PenaltyAmount = request.PenaltyAmount.Value;
            changed = true;
        }

        if (request.DiscountAmount.HasValue)
        {
            invoice.DiscountAmount = request.DiscountAmount.Value;
            changed = true;
        }

        if (request.Note is not null)
        {
            invoice.Note = request.Note;
            changed = true;
        }

        if (changed)
        {
            // Recalculate total
            invoice.TotalAmount = invoice.RentAmount + invoice.ServiceAmount + invoice.PenaltyAmount - invoice.DiscountAmount;

            // Guard: TotalAmount cannot go negative
            if (invoice.TotalAmount < 0)
                throw new BadRequestException("Discount exceeds invoice total. TotalAmount cannot be negative.");

            invoice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var paidAmount = invoice.Payments.Where(p => p.Type == PaymentType.RentPayment).Sum(p => p.Amount);

        return new InvoiceDto
        {
            Id = invoice.Id,
            ContractId = invoice.ContractId,
            RoomId = invoice.Contract!.RoomId,
            RoomNumber = invoice.Contract.Room!.RoomNumber,
            BuildingId = invoice.Contract.Room.BuildingId,
            BuildingName = invoice.Contract.Room.Building!.Name,
            TenantUserId = invoice.Contract.TenantUserId,
            TenantName = invoice.Contract.TenantUser!.FullName,
            BillingYear = invoice.BillingYear,
            BillingMonth = invoice.BillingMonth,
            RentAmount = invoice.RentAmount,
            ServiceAmount = invoice.ServiceAmount,
            PenaltyAmount = invoice.PenaltyAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = paidAmount,
            Status = invoice.Status.ToString(),
            DueDate = invoice.DueDate,
            Note = invoice.Note,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }
}

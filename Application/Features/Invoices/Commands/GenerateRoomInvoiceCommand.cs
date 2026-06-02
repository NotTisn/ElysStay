using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Invoices.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Commands;

public record GenerateRoomInvoiceCommand : IRequest<InvoiceDto>
{
    public required Guid RoomId { get; init; }
    public required int BillingYear { get; init; }
    public required int BillingMonth { get; init; }
}

public class GenerateRoomInvoiceCommandHandler : IRequestHandler<GenerateRoomInvoiceCommand, InvoiceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public GenerateRoomInvoiceCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<InvoiceDto> Handle(GenerateRoomInvoiceCommand request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken)
            ?? throw new NotFoundException("Phòng", request.RoomId);

        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        var billingPeriodStart = new DateOnly(request.BillingYear, request.BillingMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(request.BillingYear, request.BillingMonth);
        var billingPeriodEnd = new DateOnly(request.BillingYear, request.BillingMonth, daysInMonth);

        var contract = await _db.Contracts
            .Include(c => c.Room)
            .Include(c => c.ContractTenants)
                .ThenInclude(ct => ct.Tenant)
            .Where(c => c.RoomId == request.RoomId && c.Status == ContractStatus.Active)
            .Where(c =>
                (!c.TerminationDate.HasValue || c.TerminationDate >= billingPeriodStart)
                && c.MoveInDate <= billingPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy hợp đồng đang hoạt động cho phòng {room.RoomNumber} trong kỳ {request.BillingMonth}/{request.BillingYear}");

        var existingInvoice = await _db.Invoices
            .AnyAsync(i => i.ContractId == contract.Id
                          && i.BillingYear == request.BillingYear
                          && i.BillingMonth == request.BillingMonth
                          && i.Status != InvoiceStatus.Void, cancellationToken);

        if (existingInvoice)
            throw new ConflictException(
                $"Hóa đơn kỳ {request.BillingMonth}/{request.BillingYear} đã tồn tại cho phòng {room.RoomNumber}.",
                "DUPLICATE_INVOICE");

        var building = room.Building!;

        var buildingServices = await _db.Services
            .Where(s => s.BuildingId == room.BuildingId && s.IsActive)
            .ToListAsync(cancellationToken);

        var roomOverrides = (await _db.RoomServices
            .Where(rs => rs.RoomId == request.RoomId)
            .ToListAsync(cancellationToken))
            .ToDictionary(rs => rs.ServiceId);

        var meterReadings = (await _db.MeterReadings
            .Where(mr => mr.RoomId == request.RoomId
                         && mr.BillingYear == request.BillingYear
                         && mr.BillingMonth == request.BillingMonth)
            .ToListAsync(cancellationToken))
            .ToDictionary(mr => mr.ServiceId);

        var dueMonth = billingPeriodStart.AddMonths(1);
        var dueDate = new DateOnly(dueMonth.Year, dueMonth.Month,
            Math.Min(building.InvoiceDueDay, DateTime.DaysInMonth(dueMonth.Year, dueMonth.Month)));

        var built = InvoiceBuilder.Build(
            contract, buildingServices, roomOverrides, meterReadings,
            billingPeriodStart, billingPeriodEnd, dueDate);

        built.Invoice.CreatedBy = _currentUser.GetRequiredUserId();
        _db.Invoices.Add(built.Invoice);
        foreach (var li in built.LineItems) _db.InvoiceDetails.Add(li);

        await _db.SaveChangesAsync(cancellationToken);

        return InvoiceDtoMapper.MapToDto(built.Invoice, contract, building);
    }
}

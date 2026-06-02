using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Invoices.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Commands;

public record GenerateInvoicesCommand : IRequest<InvoiceGenerationResult>
{
    public required Guid BuildingId { get; init; }
    public required int BillingYear { get; init; }
    public required int BillingMonth { get; init; }
}

public class GenerateInvoicesCommandHandler : IRequestHandler<GenerateInvoicesCommand, InvoiceGenerationResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public GenerateInvoicesCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<InvoiceGenerationResult> Handle(GenerateInvoicesCommand request, CancellationToken cancellationToken)
    {
        await _buildingScope.AuthorizeAsync(request.BuildingId, cancellationToken);
        var billingPeriodStart = new DateOnly(request.BillingYear, request.BillingMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(billingPeriodStart.Year, billingPeriodStart.Month);
        var billingPeriodEnd = new DateOnly(request.BillingYear, request.BillingMonth, daysInMonth);

        var building = await _db.Buildings
            .FirstOrDefaultAsync(b => b.Id == request.BuildingId, cancellationToken)
            ?? throw new NotFoundException("Tòa nhà", request.BuildingId);
        // get the list of contracts is active in billing period
        var contracts = await _db.Contracts
            .Include(c => c.Room!)
            .Include(c => c.ContractTenants)
                .ThenInclude(ct => ct.Tenant)
            .Where(c => c.Room!.BuildingId == request.BuildingId && c.Status == ContractStatus.Active)
            .Where(c =>
                    (!c.TerminationDate.HasValue || c.TerminationDate >= billingPeriodStart)
                    && c.MoveInDate <= billingPeriodEnd
                )
            .OrderBy(c => c.StartDate)
            .ToListAsync(cancellationToken);

        // Exclude voided invoices so re-generation is possible after voiding
        var existingInvoiceContractIds = await _db.Invoices
            .Where(i => i.BillingYear == request.BillingYear &&
                        i.BillingMonth == request.BillingMonth &&
                        i.Contract!.Room!.BuildingId == request.BuildingId &&
                        i.Status != InvoiceStatus.Void)
            .Select(i => i.ContractId)
            .ToHashSetAsync(cancellationToken);

        var buildingServices = await _db.Services
            .Where(s => s.BuildingId == request.BuildingId && s.IsActive)
            .ToListAsync(cancellationToken);

        // Group overrides and meter readings by room for O(1) lookup per contract
        var overrideLookup = (await _db.RoomServices
            .Where(rs => rs.Room!.BuildingId == request.BuildingId)
            .ToListAsync(cancellationToken))
            .GroupBy(rs => rs.RoomId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, RoomService>)g.ToDictionary(rs => rs.ServiceId));

        var meterReadingsByRoom = (await _db.MeterReadings
            .Where(mr => mr.BillingYear == request.BillingYear &&
                         mr.BillingMonth == request.BillingMonth &&
                         mr.Room!.BuildingId == request.BuildingId)
            .ToListAsync(cancellationToken))
            .GroupBy(mr => mr.RoomId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, MeterReading>)g.ToDictionary(mr => mr.ServiceId));

        var dueMonth = billingPeriodStart.AddMonths(1);
        var dueDate = new DateOnly(dueMonth.Year, dueMonth.Month,
            Math.Min(building.InvoiceDueDay, DateTime.DaysInMonth(dueMonth.Year, dueMonth.Month)));

        var generated = new List<InvoiceDto>();
        var skipped = new List<SkippedContract>();
        var warnings = new List<string>();

        foreach (var contract in contracts)
        {
            if (existingInvoiceContractIds.Contains(contract.Id))
            {
                skipped.Add(new SkippedContract
                {
                    ContractId = contract.Id,
                    Reason = $"Hóa đơn đã tồn tại cho kỳ {request.BillingMonth}/{request.BillingYear}"
                });
                continue;
            }

            var roomOverrides = overrideLookup.GetValueOrDefault(contract.RoomId, new Dictionary<Guid, RoomService>());
            var roomReadings = meterReadingsByRoom.GetValueOrDefault(contract.RoomId, new Dictionary<Guid, MeterReading>());

            var built = InvoiceBuilder.Build(
                contract, buildingServices, roomOverrides, roomReadings,
                billingPeriodStart, billingPeriodEnd, dueDate);

            built.Invoice.CreatedBy = _currentUser.GetRequiredUserId();
            _db.Invoices.Add(built.Invoice);
            foreach (var li in built.LineItems) _db.InvoiceDetails.Add(li);
            warnings.AddRange(built.Warnings);
            generated.Add(InvoiceDtoMapper.MapToDto(built.Invoice, contract, building));
        }

        // IG-08: Single transaction
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (generated.Count > 0)
        {
            throw new ConflictException(
                "Hóa đơn đã tồn tại cho một số hợp đồng trong kỳ này. Vui lòng thử lại.",
                "DUPLICATE_INVOICE");
        }

        return new InvoiceGenerationResult
        {
            Generated = generated,
            Skipped = skipped,
            Warnings = warnings
        };
    }

}
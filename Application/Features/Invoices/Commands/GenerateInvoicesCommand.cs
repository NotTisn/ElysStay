using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Invoices.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Commands;

/// <summary>
/// Generates invoices for all active contracts in a building for a given month.
/// IG-01: Idempotent — skips if invoice already exists for contract+month.
/// IG-02: Missing meter reading → skip that service line + warning.
/// IG-03: Metered service: consumption × effective price.
/// IG-04: Flat service: quantity × effective price.
/// IG-06: DueDate = next month's Building.InvoiceDueDay.
/// IG-07: Status starts as DRAFT.
/// IG-08: Single DB transaction.
/// PR-05/PR-06: Prorate first/last month.
/// </summary>
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
        var userId = _currentUser.GetRequiredUserId();

        await _buildingScope.AuthorizeAsync(request.BuildingId, cancellationToken);

        var building = await _db.Buildings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BuildingId, cancellationToken)
            ?? throw new NotFoundException("Building", request.BuildingId);

        // Get all active contracts for rooms in this building
        var contracts = await _db.Contracts
            .Include(c => c.Room!)
            .Include(c => c.TenantUser!)
            .Include(c => c.ContractTenants)
            .Where(c => c.Room!.BuildingId == request.BuildingId && c.Status == ContractStatus.Active)
            .ToListAsync(cancellationToken);

        // Get existing invoices for this period (for idempotency check)
        var existingInvoiceContractIds = await _db.Invoices
            .Where(i => i.BillingYear == request.BillingYear &&
                       i.BillingMonth == request.BillingMonth &&
                       i.Contract!.Room!.BuildingId == request.BuildingId)
            .Select(i => i.ContractId)
            .ToHashSetAsync(cancellationToken);

        // Get building services
        var buildingServices = await _db.Services
            .Where(s => s.BuildingId == request.BuildingId && s.IsActive)
            .ToListAsync(cancellationToken);

        // Get all room service overrides for rooms in this building
        var roomServiceOverrides = await _db.RoomServices
            .Where(rs => rs.Room!.BuildingId == request.BuildingId)
            .ToListAsync(cancellationToken);
        var overrideLookup = roomServiceOverrides
            .GroupBy(rs => rs.RoomId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(rs => rs.ServiceId));

        // Get meter readings for this period
        var meterReadings = await _db.MeterReadings
            .Where(mr => mr.BillingYear == request.BillingYear &&
                        mr.BillingMonth == request.BillingMonth &&
                        mr.Room!.BuildingId == request.BuildingId)
            .ToDictionaryAsync(mr => (mr.RoomId, mr.ServiceId), cancellationToken);

        var billingPeriodStart = new DateOnly(request.BillingYear, request.BillingMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(request.BillingYear, request.BillingMonth);
        var billingPeriodEnd = new DateOnly(request.BillingYear, request.BillingMonth, daysInMonth);

        // Compute DueDate (IG-06): next month, Building.InvoiceDueDay
        var dueMonth = billingPeriodStart.AddMonths(1);
        var dueDayMax = DateTime.DaysInMonth(dueMonth.Year, dueMonth.Month);
        var dueDay = Math.Min(building.InvoiceDueDay, dueDayMax);
        var dueDate = new DateOnly(dueMonth.Year, dueMonth.Month, dueDay);

        var generated = new List<InvoiceDto>();
        var skipped = new List<SkippedContract>();
        var warnings = new List<string>();

        foreach (var contract in contracts)
        {
            // IG-01: Idempotent — skip if already exists
            if (existingInvoiceContractIds.Contains(contract.Id))
            {
                skipped.Add(new SkippedContract
                {
                    ContractId = contract.Id,
                    Reason = $"Invoice already exists for {request.BillingYear}/{request.BillingMonth}"
                });
                continue;
            }

            // Skip contracts terminated before billing period starts
            if (contract.TerminationDate.HasValue && contract.TerminationDate.Value < billingPeriodStart)
            {
                skipped.Add(new SkippedContract
                {
                    ContractId = contract.Id,
                    Reason = $"Contract terminated ({contract.TerminationDate.Value}) before billing period"
                });
                continue;
            }

            // Skip contracts that start after billing period ends
            if (contract.StartDate > billingPeriodEnd)
            {
                skipped.Add(new SkippedContract
                {
                    ContractId = contract.Id,
                    Reason = $"Contract starts ({contract.StartDate}) after billing period"
                });
                continue;
            }

            var lineItems = new List<InvoiceDetail>();

            // 1. RENT LINE (with proration)
            var rentAmount = CalculateRentAmount(contract, billingPeriodStart, billingPeriodEnd, daysInMonth);
            lineItems.Add(new InvoiceDetail
            {
                InvoiceId = Guid.Empty, // set after invoice creation
                Description = "Tiền phòng",
                Quantity = 1,
                UnitPrice = rentAmount,
                Amount = rentAmount
            });

            // Get active occupant count for flat services
            var activeOccupantCount = contract.ContractTenants
                .Count(ct => ct.MoveOutDate == null);

            // 2. SERVICE LINES
            foreach (var service in buildingServices)
            {
                // Check room-level override
                var hasOverride = overrideLookup.TryGetValue(contract.RoomId, out var roomOverrides) &&
                                  roomOverrides.TryGetValue(service.Id, out var roomService);
                var roomServiceOverride = hasOverride ? overrideLookup[contract.RoomId][service.Id] : null;

                // IsEnabled check
                if (roomServiceOverride?.IsEnabled == false)
                    continue;

                var effectivePrice = roomServiceOverride?.OverrideUnitPrice ?? service.UnitPrice;

                if (service.IsMetered)
                {
                    // IG-03: Metered service
                    if (!meterReadings.TryGetValue((contract.RoomId, service.Id), out var reading))
                    {
                        // IG-02: Missing meter reading — skip + warning
                        warnings.Add($"Room {contract.Room!.RoomNumber}: Missing meter reading for '{service.Name}' ({request.BillingYear}/{request.BillingMonth})");
                        continue;
                    }

                    var consumption = reading.Consumption;
                    var amount = consumption * effectivePrice;

                    lineItems.Add(new InvoiceDetail
                    {
                        InvoiceId = Guid.Empty,
                        ServiceId = service.Id,
                        Description = service.Name,
                        Quantity = consumption,
                        UnitPrice = effectivePrice,
                        Amount = amount,
                        PreviousReading = reading.PreviousReading,
                        CurrentReading = reading.CurrentReading
                    });
                }
                else
                {
                    // IG-04: Flat service
                    var quantity = roomServiceOverride?.OverrideQuantity
                        ?? (activeOccupantCount > 0 ? activeOccupantCount : 1);

                    // Warn if defaulting to 1 due to 0 active occupants
                    if (roomServiceOverride?.OverrideQuantity == null && activeOccupantCount == 0)
                    {
                        warnings.Add($"Room {contract.Room!.RoomNumber}: 0 active occupants for flat service '{service.Name}', defaulting quantity to 1");
                    }

                    var amount = quantity * effectivePrice;

                    lineItems.Add(new InvoiceDetail
                    {
                        InvoiceId = Guid.Empty,
                        ServiceId = service.Id,
                        Description = service.Name,
                        Quantity = quantity,
                        UnitPrice = effectivePrice,
                        Amount = amount
                    });
                }
            }

            // 3. CREATE INVOICE
            var serviceAmount = lineItems.Where(li => li.ServiceId != null).Sum(li => li.Amount);

            var invoice = new Invoice
            {
                ContractId = contract.Id,
                BillingYear = request.BillingYear,
                BillingMonth = request.BillingMonth,
                RentAmount = rentAmount,
                ServiceAmount = serviceAmount,
                PenaltyAmount = 0,
                DiscountAmount = 0,
                TotalAmount = rentAmount + serviceAmount,
                Status = InvoiceStatus.Draft,
                DueDate = dueDate,
                CreatedBy = userId
            };
            _db.Invoices.Add(invoice);

            // Set InvoiceId on line items
            foreach (var li in lineItems)
            {
                li.InvoiceId = invoice.Id;
                _db.InvoiceDetails.Add(li);
            }

            generated.Add(new InvoiceDto
            {
                Id = invoice.Id,
                ContractId = invoice.ContractId,
                RoomId = contract.RoomId,
                RoomNumber = contract.Room!.RoomNumber,
                BuildingId = request.BuildingId,
                BuildingName = building.Name,
                TenantUserId = contract.TenantUserId,
                TenantName = contract.TenantUser!.FullName,
                BillingYear = invoice.BillingYear,
                BillingMonth = invoice.BillingMonth,
                RentAmount = invoice.RentAmount,
                ServiceAmount = invoice.ServiceAmount,
                PenaltyAmount = invoice.PenaltyAmount,
                DiscountAmount = invoice.DiscountAmount,
                TotalAmount = invoice.TotalAmount,
                PaidAmount = 0,
                Status = invoice.Status.ToString(),
                DueDate = invoice.DueDate,
                Note = invoice.Note,
                CreatedAt = invoice.CreatedAt,
                UpdatedAt = invoice.UpdatedAt
            });
        }

        // IG-08: Single transaction
        await _db.SaveChangesAsync(cancellationToken);

        return new InvoiceGenerationResult
        {
            Generated = generated,
            Skipped = skipped,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Calculates rent with proration for first/last month (PR-05/PR-06).
    /// </summary>
    private static decimal CalculateRentAmount(Contract contract, DateOnly periodStart, DateOnly periodEnd, int daysInMonth)
    {
        var effectiveStart = periodStart;
        var effectiveEnd = periodEnd;

        // PR-05: Prorate first month (based on MoveInDate)
        if (contract.MoveInDate > periodStart && contract.MoveInDate <= periodEnd)
        {
            effectiveStart = contract.MoveInDate;
        }

        // PR-06: Prorate last month (based on TerminationDate)
        if (contract.TerminationDate.HasValue &&
            contract.TerminationDate.Value >= periodStart &&
            contract.TerminationDate.Value < periodEnd)
        {
            effectiveEnd = contract.TerminationDate.Value;
        }

        // If effective range covers full month, return full rent
        if (effectiveStart == periodStart && effectiveEnd == periodEnd)
            return contract.MonthlyRent;

        // Prorated: MonthlyRent / daysInMonth × days
        var days = effectiveEnd.DayNumber - effectiveStart.DayNumber + 1;
        return Math.Round(contract.MonthlyRent / daysInMonth * days, 0);
    }
}

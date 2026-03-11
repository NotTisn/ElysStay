using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MeterReadings.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MeterReadings.Commands;

/// <summary>
/// Bulk upsert meter readings for a building/month.
/// UQ-03: Unique(RoomId, ServiceId, BillingYear, BillingMonth).
/// VAL-04: CurrentReading ≥ PreviousReading.
/// </summary>
public record BulkUpsertMeterReadingsCommand : IRequest<IReadOnlyList<MeterReadingDto>>
{
    public required Guid BuildingId { get; init; }
    public required int BillingYear { get; init; }
    public required int BillingMonth { get; init; }
    public required IReadOnlyList<MeterReadingEntry> Readings { get; init; }
}

public record MeterReadingEntry
{
    public required Guid RoomId { get; init; }
    public required Guid ServiceId { get; init; }
    public decimal? PreviousReading { get; init; }
    public required decimal CurrentReading { get; init; }
}

public class BulkUpsertMeterReadingsCommandHandler : IRequestHandler<BulkUpsertMeterReadingsCommand, IReadOnlyList<MeterReadingDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public BulkUpsertMeterReadingsCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<IReadOnlyList<MeterReadingDto>> Handle(BulkUpsertMeterReadingsCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        await _buildingScope.AuthorizeAsync(request.BuildingId, cancellationToken);

        // Verify building exists
        var buildingExists = await _db.Buildings.AnyAsync(b => b.Id == request.BuildingId, cancellationToken);
        if (!buildingExists)
            throw new NotFoundException("Building", request.BuildingId);

        // Validate all room IDs belong to this building
        var requestedRoomIds = request.Readings.Select(r => r.RoomId).Distinct().ToList();
        var validRoomIds = await _db.Rooms
            .Where(r => r.BuildingId == request.BuildingId && requestedRoomIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var invalidRoomIds = requestedRoomIds.Except(validRoomIds).ToList();
        if (invalidRoomIds.Count > 0)
            throw new BadRequestException($"Rooms [{string.Join(", ", invalidRoomIds)}] do not belong to this building.");

        // Validate all service IDs are metered services of this building
        var requestedServiceIds = request.Readings.Select(r => r.ServiceId).Distinct().ToList();
        var validServiceIds = await _db.Services
            .Where(s => s.BuildingId == request.BuildingId && s.IsMetered && requestedServiceIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var invalidServiceIds = requestedServiceIds.Except(validServiceIds).ToList();
        if (invalidServiceIds.Count > 0)
            throw new BadRequestException($"Services [{string.Join(", ", invalidServiceIds)}] are not valid metered services for this building.");

        // Compute the previous billing period for auto-fetching PreviousReading
        var prevYear = request.BillingMonth == 1 ? request.BillingYear - 1 : request.BillingYear;
        var prevMonth = request.BillingMonth == 1 ? 12 : request.BillingMonth - 1;

        // Load prior month readings for auto-fill
        var priorReadings = await _db.MeterReadings
            .Where(mr => mr.BillingYear == prevYear && mr.BillingMonth == prevMonth &&
                         mr.Room!.BuildingId == request.BuildingId)
            .ToDictionaryAsync(mr => (mr.RoomId, mr.ServiceId), mr => mr.CurrentReading, cancellationToken);

        // Load existing readings for this period (for upsert)
        var existingReadings = await _db.MeterReadings
            .Where(mr => mr.BillingYear == request.BillingYear && mr.BillingMonth == request.BillingMonth &&
                         mr.Room!.BuildingId == request.BuildingId)
            .ToDictionaryAsync(mr => (mr.RoomId, mr.ServiceId), cancellationToken);

        var results = new List<MeterReading>();

        foreach (var entry in request.Readings)
        {
            // Auto-fetch previous reading if not provided
            var previousReading = entry.PreviousReading
                ?? (priorReadings.TryGetValue((entry.RoomId, entry.ServiceId), out var prior) ? prior : 0);

            // VAL-04: CurrentReading >= PreviousReading
            if (entry.CurrentReading < previousReading)
                throw new BadRequestException(
                    $"CurrentReading ({entry.CurrentReading}) must be >= PreviousReading ({previousReading}) for room {entry.RoomId}, service {entry.ServiceId}.");

            var consumption = entry.CurrentReading - previousReading;

            if (existingReadings.TryGetValue((entry.RoomId, entry.ServiceId), out var existing))
            {
                // UPDATE
                existing.PreviousReading = previousReading;
                existing.CurrentReading = entry.CurrentReading;
                existing.Consumption = consumption;
                existing.UpdatedAt = DateTime.UtcNow;
                results.Add(existing);
            }
            else
            {
                // INSERT
                var reading = new MeterReading
                {
                    RoomId = entry.RoomId,
                    ServiceId = entry.ServiceId,
                    BillingYear = request.BillingYear,
                    BillingMonth = request.BillingMonth,
                    PreviousReading = previousReading,
                    CurrentReading = entry.CurrentReading,
                    Consumption = consumption,
                    DateRead = DateTime.UtcNow,
                    CreatedBy = userId
                };
                _db.MeterReadings.Add(reading);
                results.Add(reading);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties for DTO mapping
        var resultIds = results.Select(r => r.Id).ToHashSet();
        return await _db.MeterReadings
            .AsNoTracking()
            .Where(mr => resultIds.Contains(mr.Id))
            .Select(mr => new MeterReadingDto
            {
                Id = mr.Id,
                RoomId = mr.RoomId,
                RoomNumber = mr.Room!.RoomNumber,
                ServiceId = mr.ServiceId,
                ServiceName = mr.Service!.Name,
                ServiceUnit = mr.Service.Unit,
                BillingYear = mr.BillingYear,
                BillingMonth = mr.BillingMonth,
                PreviousReading = mr.PreviousReading,
                CurrentReading = mr.CurrentReading,
                Consumption = mr.Consumption,
                CreatedAt = mr.CreatedAt,
                UpdatedAt = mr.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }
}

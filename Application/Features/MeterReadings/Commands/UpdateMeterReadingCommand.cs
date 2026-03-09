using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MeterReadings.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MeterReadings.Commands;

/// <summary>
/// Edit a single meter reading.
/// Owner/Staff only.
/// </summary>
public record UpdateMeterReadingCommand : IRequest<MeterReadingDto>
{
    public Guid Id { get; init; }
    public decimal? PreviousReading { get; init; }
    public decimal? CurrentReading { get; init; }
}

public class UpdateMeterReadingCommandHandler : IRequestHandler<UpdateMeterReadingCommand, MeterReadingDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public UpdateMeterReadingCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<MeterReadingDto> Handle(UpdateMeterReadingCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var reading = await _db.MeterReadings
            .Include(mr => mr.Room!).ThenInclude(r => r.Building!)
            .Include(mr => mr.Service!)
            .FirstOrDefaultAsync(mr => mr.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Meter reading", request.Id);

        await _buildingScope.AuthorizeAsync(reading.Room!.BuildingId, cancellationToken);

        if (request.PreviousReading.HasValue)
            reading.PreviousReading = request.PreviousReading.Value;

        if (request.CurrentReading.HasValue)
            reading.CurrentReading = request.CurrentReading.Value;

        // VAL-04: CurrentReading >= PreviousReading
        if (reading.CurrentReading < reading.PreviousReading)
            throw new BadRequestException("CurrentReading must be >= PreviousReading.");

        reading.Consumption = reading.CurrentReading - reading.PreviousReading;
        reading.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return new MeterReadingDto
        {
            Id = reading.Id,
            RoomId = reading.RoomId,
            RoomNumber = reading.Room!.RoomNumber,
            ServiceId = reading.ServiceId,
            ServiceName = reading.Service!.Name,
            ServiceUnit = reading.Service.Unit,
            BillingYear = reading.BillingYear,
            BillingMonth = reading.BillingMonth,
            PreviousReading = reading.PreviousReading,
            CurrentReading = reading.CurrentReading,
            Consumption = reading.Consumption,
            CreatedAt = reading.CreatedAt,
            UpdatedAt = reading.UpdatedAt
        };
    }
}

using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Rooms.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rooms.Commands;

public class ChangeRoomStatusCommandHandler : IRequestHandler<ChangeRoomStatusCommand, RoomDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public ChangeRoomStatusCommandHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<RoomDto> Handle(ChangeRoomStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RoomStatus>(request.Status, true, out var targetStatus))
            throw new BadRequestException($"Trạng thái không hợp lệ: '{request.Status}'. Phải là 'Available' hoặc 'Maintenance'.");

        // SM-05: Only Available and Maintenance are valid targets for manual PATCH
        if (targetStatus is not (RoomStatus.Available or RoomStatus.Maintenance))
            throw new BadRequestException("Thay đổi trạng thái thủ công chỉ hỗ trợ Trống hoặc Bảo trì.");

        var room = await _db.Rooms
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phòng {request.Id}.");

        // AUTH-05
        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        // SM-05: Current status must also be Available or Maintenance
        if (room.Status is not (RoomStatus.Available or RoomStatus.Maintenance))
            throw new ConflictException(
                $"Không thể chuyển trạng thái thủ công từ {room.Status}. Phòng phải ở trạng thái Trống hoặc Bảo trì.",
                "INVALID_STATUS_TRANSITION");

        if (room.Status == targetStatus)
            throw new BadRequestException($"Phòng đã ở trạng thái {targetStatus}.");

        room.Status = targetStatus;
        room.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Phòng đã bị thay đổi bởi thao tác khác. Vui lòng thử lại.",
                "CONCURRENCY_CONFLICT");
        }

        return new RoomDto
        {
            Id = room.Id,
            BuildingId = room.BuildingId,
            BuildingName = room.Building?.Name,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            Area = room.Area,
            Price = room.Price,
            MaxOccupants = room.MaxOccupants,
            Description = room.Description,
            Status = room.Status.ToString(),
            Images = room.Images,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt
        };
    }
}

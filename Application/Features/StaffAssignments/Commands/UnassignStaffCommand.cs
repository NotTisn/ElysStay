using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StaffAssignments.Commands;

public record UnassignStaffCommand(Guid BuildingId, Guid StaffId) : IRequest;

public class UnassignStaffCommandHandler : IRequestHandler<UnassignStaffCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UnassignStaffCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UnassignStaffCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Chỉ chủ nhà mới có thể hủy phân công nhân viên.");

        var userId = _currentUser.GetRequiredUserId();

        // Verify ownership
        var building = await _db.Buildings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BuildingId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tòa nhà {request.BuildingId}.");

        if (building.OwnerId != userId)
            throw new ForbiddenException("Bạn không sở hữu tòa nhà này.");

        var assignment = await _db.StaffAssignments
            .FirstOrDefaultAsync(sa => sa.BuildingId == request.BuildingId && sa.StaffId == request.StaffId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phân công nhân viên.");

        _db.StaffAssignments.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

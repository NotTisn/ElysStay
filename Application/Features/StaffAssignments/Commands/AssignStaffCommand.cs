using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.StaffAssignments.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StaffAssignments.Commands;

public record AssignStaffCommand : IRequest<StaffAssignmentDto>
{
    public Guid BuildingId { get; init; }
    public Guid StaffId { get; init; }
}

public class AssignStaffCommandHandler : IRequestHandler<AssignStaffCommand, StaffAssignmentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AssignStaffCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StaffAssignmentDto> Handle(AssignStaffCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only the owner can assign staff.");

        var userId = _currentUser.GetRequiredUserId();

        // Verify building exists and is owned by current user
        var building = await _db.Buildings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BuildingId, cancellationToken)
            ?? throw new NotFoundException($"Building {request.BuildingId} not found.");

        if (building.OwnerId != userId)
            throw new ForbiddenException("You do not own this building.");

        // Verify staff user exists and has Staff role
        var staffUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.StaffId, cancellationToken)
            ?? throw new NotFoundException($"User {request.StaffId} not found.");

        if (staffUser.Role != UserRole.Staff)
            throw new BadRequestException("User is not a staff member.");

        // UQ-06: Check uniqueness
        var alreadyAssigned = await _db.StaffAssignments
            .AnyAsync(sa => sa.BuildingId == request.BuildingId && sa.StaffId == request.StaffId, cancellationToken);

        if (alreadyAssigned)
            throw new ConflictException(
                "This staff member is already assigned to this building.",
                "DUPLICATE_STAFF_ASSIGNMENT");

        var assignment = new StaffAssignment
        {
            BuildingId = request.BuildingId,
            StaffId = request.StaffId
        };

        _db.StaffAssignments.Add(assignment);
        await _db.SaveChangesAsync(cancellationToken);

        return new StaffAssignmentDto
        {
            StaffId = staffUser.Id,
            Email = staffUser.Email,
            FullName = staffUser.FullName,
            Phone = staffUser.Phone,
            AssignedAt = assignment.AssignedAt
        };
    }
}

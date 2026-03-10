using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Users.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Queries;

/// <summary>
/// GET /users/{id} — Returns a single user's detail.
/// Auth: Owner sees anyone. Staff sees tenants + self. Tenant sees self only.
/// </summary>
public record GetUserByIdQuery(Guid Id) : IRequest<UserDto>;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserByIdQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new NotFoundException("User", request.Id);

        var callerId = _currentUser.GetRequiredUserId();

        // Tenant: self only
        if (_currentUser.IsTenant && user.Id != callerId)
            throw new ForbiddenException("Tenants can only view their own profile.");

        // Staff: tenants + self
        if (_currentUser.IsStaff && user.Id != callerId && user.Role != UserRole.Tenant)
            throw new ForbiddenException("Staff can only view tenant details.");

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt
        };
    }
}

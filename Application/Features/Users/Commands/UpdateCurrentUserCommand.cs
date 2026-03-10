using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Users.DTOs;
using MediatR;

namespace Application.Features.Users.Commands;

/// <summary>
/// PUT /users/me — Update the authenticated user's own name and phone.
/// Auth: ALL authenticated users.
/// </summary>
public record UpdateCurrentUserCommand : IRequest<UserProfileDto>
{
    public string? FullName { get; init; }
    public string? Phone { get; init; }
}

public class UpdateCurrentUserCommandHandler : IRequestHandler<UpdateCurrentUserCommand, UserProfileDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateCurrentUserCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserProfileDto> Handle(UpdateCurrentUserCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("User", userId);

        if (request.FullName is not null)
            user.FullName = request.FullName.Trim();

        if (request.Phone is not null)
            user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}

using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Commands;
    public record UpdateContractCommand : IRequest<ContractDto>
{
    public Guid Id { get; init; }
    public DateOnly? EndDate { get; init; }
    public decimal? MonthlyRent { get; init; }
    public string? Note { get; init; }
}
public class UpdateContractCommandHandler : IRequestHandler<UpdateContractCommand, ContractDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public UpdateContractCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }


    public async Task<ContractDto> Handle(
    UpdateContractCommand request,
    CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var contract = await _db.Contracts
            .Include(c => c.Room!)
                .ThenInclude(r => r.Building!)
            .Include(c => c.ContractTenants)
                .ThenInclude(ct => ct.Tenant!)
            .Include(c => c.Creator!)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng", request.Id);

        if (contract.Status != ContractStatus.Active)
            throw new ConflictException("Chỉ có thể cập nhật hợp đồng đang hoạt động.");

        await _buildingScope.AuthorizeAsync(contract.Room!.BuildingId, cancellationToken);
        // Status transitions for contracts are handled by dedicated commands:
        //   Active → Terminated : TerminateContractCommand (with deposit refund + room status)
        //   Active → Renewed    : RenewContractCommand (creates new contract)
        // UpdateContractCommand intentionally does not support status changes.
        // Gia han hop dong 
        if (request.EndDate is not null)
        {
            if (request.EndDate.Value <= contract.StartDate)
                throw new BadRequestException("Ngày kết thúc phải sau ngày bắt đầu.");

            contract.EndDate = request.EndDate.Value;
        }

        if (request.MonthlyRent is not null)
        {
            if (request.MonthlyRent.Value <= 0)
                throw new BadRequestException("Tiền thuê hàng tháng phải lớn hơn 0.");

            contract.MonthlyRent = request.MonthlyRent.Value;
        }

        if (request.Note is not null)
            contract.Note = request.Note;

        contract.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var mainTenant = contract.ContractTenants.FirstOrDefault(ct => ct.IsMainTenant)
            ?? throw new InvalidOperationException("Hợp đồng không có tenant chính.");
        return new ContractDto
        {
            Id = contract.Id,
            RoomId = contract.RoomId,
            RoomNumber = contract.Room.RoomNumber,
            BuildingId = contract.Room.BuildingId,
            BuildingName = contract.Room.Building!.Name,
            TenantUserId = mainTenant.TenantUserId,
            TenantName = mainTenant.Tenant!.FullName,
            ReservationId = contract.ReservationId,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MoveInDate = contract.MoveInDate,
            MonthlyRent = contract.MonthlyRent,
            DepositAmount = contract.DepositAmount,
            DepositStatus = contract.DepositStatus.ToString(),
            Status = contract.Status.ToString(),
            TerminationDate = contract.TerminationDate,
            TerminationNote = contract.TerminationNote,
            RefundAmount = contract.RefundAmount,
            CreatedBy = contract.CreatedBy,
            Note = contract.Note,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt
        };
    }
}

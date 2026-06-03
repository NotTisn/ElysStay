using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Queries;

/// <summary>
/// GET /contracts/{id}/pdf — Generate and return a PDF for the given contract.
/// Auth: ALL authenticated users (building-scope enforced by contract ownership).
/// </summary>
public record ExportContractPdfQuery(Guid ContractId) : IRequest<ContractPdfResult>;

public record ContractPdfResult(byte[] PdfBytes, string FileName);

public class ExportContractPdfQueryHandler : IRequestHandler<ExportContractPdfQuery, ContractPdfResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IContractPdfService _pdfService;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public ExportContractPdfQueryHandler(IApplicationDbContext db, IContractPdfService pdfService, ICurrentUserService currentUser, IBuildingScopeService buildingScope)
    {
        _db = db;
        _pdfService = pdfService;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ContractPdfResult> Handle(ExportContractPdfQuery request, CancellationToken ct)
    {
        var contract = await _db.Contracts
            .AsNoTracking()
            .Include(c => c.Room!)
                .ThenInclude(r => r.Building!)
                    .ThenInclude(b => b.Owner)
            .Include(c => c.TenantUser)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, ct)
            ?? throw new NotFoundException("Hợp đồng", request.ContractId);

        var userId = _currentUser.GetRequiredUserId();
        
        if (_currentUser.IsTenant)
        {
            var isOnContract = contract.TenantUserId == userId ||
                await _db.ContractTenants.AnyAsync(
                    ct2 => ct2.ContractId == contract.Id && ct2.TenantUserId == userId, ct);
            if (!isOnContract)
                throw new ForbiddenException("Bạn chỉ có thể xem hợp đồng của mình.");
        }
        else
        {
            await _buildingScope.AuthorizeAsync(contract.Room!.BuildingId, ct);
        }

        var building = contract.Room!.Building!;
        var owner = building.Owner!;
        var tenant = contract.TenantUser!;

        var data = new ContractPdfData
        {
            BuildingName = building.Name,
            RoomNumber = contract.Room!.RoomNumber,
            OwnerName = owner.FullName,
            TenantName = tenant.FullName,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MonthlyRent = contract.MonthlyRent,
            DepositAmount = contract.DepositAmount,
            Note = contract.Note
        };

        var pdfBytes = _pdfService.Generate(data);
        var fileName = $"HopDong_{building.Name}_{contract.Room.RoomNumber}_{contract.StartDate:yyyyMMdd}.pdf";

        return new ContractPdfResult(pdfBytes, fileName);
    }
}

using Application.Common.Interfaces;
using Application.Features.Buildings.DTOs;
using Domain.Entities;
using MediatR;

namespace Application.Features.Buildings.Commands;

public class CreateBuildingCommandHandler : IRequestHandler<CreateBuildingCommand, BuildingDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingDefaultsProvider _defaults;

    public CreateBuildingCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IBuildingDefaultsProvider defaults)
    {
        _db = db;
        _currentUser = currentUser;
        _defaults = defaults;
    }

    public async Task<BuildingDto> Handle(CreateBuildingCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var building = new Building
        {
            OwnerId = userId,
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            Description = request.Description?.Trim(),
            TotalFloors = request.TotalFloors,
            InvoiceDueDay = request.InvoiceDueDay
        };

        _db.Buildings.Add(building);

        // BD-01: Auto-create default services
        var defaultServices = GetDefaultServices(building.Id);
        _db.Services.AddRange(defaultServices);

        await _db.SaveChangesAsync(cancellationToken);

        return new BuildingDto
        {
            Id = building.Id,
            OwnerId = building.OwnerId,
            Name = building.Name,
            Address = building.Address,
            Description = building.Description,
            TotalFloors = building.TotalFloors,
            InvoiceDueDay = building.InvoiceDueDay,
            CreatedAt = building.CreatedAt,
            UpdatedAt = building.UpdatedAt
        };
    }

    private List<Service> GetDefaultServices(Guid buildingId)
    {
        // BD-01: Default pricing from typed configuration
        return
        [
            new Service
            {
                BuildingId = buildingId,
                Name = "Tiền điện",
                Unit = "kWh",
                UnitPrice = _defaults.ElectricityPrice,
                IsMetered = true
            },
            new Service
            {
                BuildingId = buildingId,
                Name = "Tiền nước",
                Unit = "m³",
                UnitPrice = _defaults.WaterPrice,
                IsMetered = true
            },
            new Service
            {
                BuildingId = buildingId,
                Name = "Internet",
                Unit = "tháng",
                UnitPrice = _defaults.InternetPrice,
                IsMetered = false
            },
            new Service
            {
                BuildingId = buildingId,
                Name = "Rác",
                Unit = "tháng",
                UnitPrice = _defaults.GarbagePrice,
                IsMetered = false
            },
            new Service
            {
                BuildingId = buildingId,
                Name = "Gửi xe",
                Unit = "người/tháng",
                UnitPrice = _defaults.ParkingPrice,
                IsMetered = false
            }
        ];
    }
}

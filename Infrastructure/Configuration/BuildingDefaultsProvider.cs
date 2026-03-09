using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Configuration;

/// <summary>
/// Reads default building service prices from IConfiguration.
/// Section: "Building:DefaultServices". Falls back to spec defaults.
/// </summary>
public class BuildingDefaultsProvider : IBuildingDefaultsProvider
{
    public decimal ElectricityPrice { get; }
    public decimal WaterPrice { get; }
    public decimal InternetPrice { get; }
    public decimal GarbagePrice { get; }
    public decimal ParkingPrice { get; }

    public BuildingDefaultsProvider(IConfiguration configuration)
    {
        var section = configuration.GetSection("Building:DefaultServices");

        ElectricityPrice = section.GetValue("ElectricityPrice", 3500m);
        WaterPrice = section.GetValue("WaterPrice", 20000m);
        InternetPrice = section.GetValue("InternetPrice", 100000m);
        GarbagePrice = section.GetValue("GarbagePrice", 20000m);
        ParkingPrice = section.GetValue("ParkingPrice", 100000m);
    }
}

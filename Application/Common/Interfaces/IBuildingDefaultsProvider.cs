namespace Application.Common.Interfaces;

/// <summary>
/// Provides default building service configuration (prices, names).
/// Implementation in Infrastructure reads from IConfiguration.
/// </summary>
public interface IBuildingDefaultsProvider
{
    decimal ElectricityPrice { get; }
    decimal WaterPrice { get; }
    decimal InternetPrice { get; }
    decimal GarbagePrice { get; }
    decimal ParkingPrice { get; }
}

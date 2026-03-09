namespace Application.Features.Dashboard.DTOs;

/// <summary>
/// Owner dashboard summary data.
/// </summary>
public record OwnerDashboardDto(
    int TotalBuildings,
    int TotalRooms,
    int OccupiedRooms,
    decimal OccupancyRate,
    int ActiveContracts,
    int OverdueInvoiceCount,
    decimal OverdueAmount,
    decimal MonthlyRevenue);

/// <summary>
/// Staff dashboard summary data.
/// </summary>
public record StaffDashboardDto(
    int AssignedBuildings,
    int PendingIssues,
    int PendingMeterReadings);

/// <summary>
/// Tenant dashboard summary data.
/// </summary>
public record TenantDashboardDto(
    Guid? RoomId,
    string? RoomNumber,
    string? BuildingName,
    string? ContractStatus,
    DateOnly? ContractEndDate,
    int UnpaidInvoiceCount,
    decimal UnpaidAmount,
    int OpenIssueCount);

/// <summary>
/// GET /reports/dashboard-stats response.
/// </summary>
public record DashboardStatsDto(
    int TotalRooms,
    int OccupiedRooms,
    decimal OccupancyRate,
    int ActiveContracts,
    int OverdueContracts,
    int OverdueInvoiceCount,
    decimal OverdueAmount,
    decimal MonthlyRevenue);

/// <summary>
/// GET /reports/pnl response.
/// </summary>
public record PnlReportDto(
    Guid? BuildingId,
    int Year,
    List<PnlMonthDto> Months);

public record PnlMonthDto(
    int Month,
    decimal OperationalIncome,
    decimal DepositsReceived,
    decimal DepositsRefunded,
    decimal Expenses,
    decimal NetOperational,
    decimal NetCashFlow);

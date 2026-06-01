using Application.Features.Invoices.DTOs;
using Domain.Entities;

namespace Application.Features.Invoices.Commands;

internal static class InvoiceDtoMapper
{
    internal static InvoiceDto MapToDto(Invoice invoice, Contract contract, Building building, decimal paidAmount = 0)
    {
        var mainTenant = contract.ContractTenants.FirstOrDefault(ct => ct.IsMainTenant)
            ?? throw new InvalidOperationException($"Hợp đồng {contract.Id} không có người thuê chính.");
        return new InvoiceDto
        {
            Id = invoice.Id,
            ContractId = invoice.ContractId,
            RoomId = contract.RoomId,
            RoomNumber = contract.Room!.RoomNumber,
            BuildingId = building.Id,
            BuildingName = building.Name,
            TenantUserId = mainTenant.TenantUserId,
            TenantName = mainTenant.Tenant!.FullName,
            BillingYear = invoice.BillingYear,
            BillingMonth = invoice.BillingMonth,
            RentAmount = invoice.RentAmount,
            ServiceAmount = invoice.ServiceAmount,
            PenaltyAmount = invoice.PenaltyAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = paidAmount,
            Status = invoice.Status.ToString(),
            DueDate = invoice.DueDate,
            Note = invoice.Note,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }
}

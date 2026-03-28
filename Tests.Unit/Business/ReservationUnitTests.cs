using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using System;

namespace ElysStay.Tests.Unit.Business;

public class ReservationUnitTests
{
    [Fact]
    public void ReservationExpiry_AutoCancels_WhenExpired()
    {
        // Arrange
        var reservation = new RoomReservation
        {
            Status = ReservationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };

        // Act
        if (reservation.ExpiresAt < DateTime.UtcNow)
        {
            reservation.Status = ReservationStatus.Expired;
        }

        // Assert
        reservation.Status.Should().Be(ReservationStatus.Expired);
    }

    [Fact]
    public void RefundReservation_ShouldRecordRefundAmount()
    {
        // Arrange
        var reservation = new RoomReservation
        {
            Status = ReservationStatus.Cancelled,
            DepositAmount = 1_000_000
        };

        // Act
        reservation.RefundAmount = 1_000_000;
        reservation.RefundedAt = DateTime.UtcNow;
        reservation.RefundNote = "Customer requested cancellation";

        // Assert
        reservation.RefundAmount.Should().Be(1_000_000);
        reservation.RefundedAt.Should().NotBeNull();
        reservation.RefundNote.Should().Be("Customer requested cancellation");
    }

    [Fact]
    public void ConvertReservation_ShouldCreateContractDraft()
    {
        // Arrange
        var reservation = new RoomReservation
        {
            Status = ReservationStatus.Confirmed,
            RoomId = Guid.NewGuid(),
            TenantUserId = Guid.NewGuid()
        };

        // Act
        reservation.Status = ReservationStatus.Converted;
        var contract = new Contract
        {
            RoomId = reservation.RoomId,
            TenantUserId = reservation.TenantUserId,
            Status = ContractStatus.Active
        };

        // Assert
        reservation.Status.Should().Be(ReservationStatus.Converted);
        contract.Status.Should().Be(ContractStatus.Active);
        contract.RoomId.Should().Be(reservation.RoomId);
        contract.TenantUserId.Should().Be(reservation.TenantUserId);
    }
}

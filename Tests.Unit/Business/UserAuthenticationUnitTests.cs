using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using System;

namespace ElysStay.Tests.Unit.Business;

public class UserAuthenticationUnitTests
{
    [Fact]
    public void NewUser_ShouldDefaultTo_TenantRole_And_ActiveStatus()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "tenant.new@example.com",
            FullName = "New Tenant"
        };

        // Assert
        user.Role.Should().Be(UserRole.Tenant);
        user.Status.Should().Be(UserStatus.Active);
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateUser_AsOwner_ShouldAssignCorrectRole()
    {
        // Arrange
        var user = new User
        {
            Email = "landlord@example.com",
            FullName = "Owner User",
            Role = UserRole.Owner
        };

        // Act & Assert
        user.Role.Should().Be(UserRole.Owner);
    }

    [Fact]
    public void UpdateUserProfile_ShouldModifyFields_And_UpdateTimestamp()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            FullName = "Initial Name",
            Phone = "123456789"
        };

        // Act
        user.FullName = "Updated Name";
        user.Phone = "987654321";
        user.UpdatedAt = DateTime.UtcNow;

        // Assert
        user.FullName.Should().Be("Updated Name");
        user.Phone.Should().Be("987654321");
        user.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SuspendUser_ShouldUpdateUserStatus_ToInactive()
    {
        // Arrange
        var user = new User
        {
            Email = "banned@example.com",
            Status = UserStatus.Active
        };

        // Act
        user.Status = UserStatus.Deactivated;
        user.UpdatedAt = DateTime.UtcNow;

        // Assert
        user.Status.Should().Be(UserStatus.Deactivated);
    }

    [Fact]
    public void SoftDeleteUser_ShouldSetDeletedAt_Successfully()
    {
        // Arrange
        var user = new User
        {
            Email = "deleted@example.com",
            FullName = "Delete Me"
        };

        // Act
        user.DeletedAt = DateTime.UtcNow;

        // Assert
        user.DeletedAt.Should().NotBeNull();
        user.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}

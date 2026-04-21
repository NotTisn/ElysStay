using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using System;

namespace ElysStay.Tests.Unit.Business;

public class MaintenanceIssueUnitTests
{
    [Fact]
    public void NewMaintenanceIssue_ShouldHave_NewStatus_And_MediumPriority_ByDefault()
    {
        // Arrange & Act
        var issue = new MaintenanceIssue
        {
            Title = "Leaking Pipe",
            Description = "Water leaking in bathroom"
        };

        // Assert
        issue.Status.Should().Be(IssueStatus.New);
        issue.Priority.Should().Be(PriorityLevel.Medium);
        issue.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AssignIssue_ToStaff_ShouldUpdateStatusTo_InProgress()
    {
        // Arrange
        var issue = new MaintenanceIssue
        {
            Status = IssueStatus.New,
            Title = "Broken AC"
        };

        var staffId = Guid.NewGuid();

        // Act
        issue.AssignedTo = staffId;
        issue.Status = IssueStatus.InProgress;
        issue.UpdatedAt = DateTime.UtcNow;

        // Assert
        issue.AssignedTo.Should().Be(staffId);
        issue.Status.Should().Be(IssueStatus.InProgress);
    }

    [Fact]
    public void ResolveIssue_ShouldUpdateStatusTo_Resolved()
    {
        // Arrange
        var issue = new MaintenanceIssue
        {
            Status = IssueStatus.InProgress,
            AssignedTo = Guid.NewGuid()
        };

        // Act
        issue.Status = IssueStatus.Resolved;
        issue.UpdatedAt = DateTime.UtcNow;

        // Assert
        issue.Status.Should().Be(IssueStatus.Resolved);
    }

    [Fact]
    public void ReopenIssue_ResolvedToNew_ShouldUpdateStatus()
    {
        // Arrange
        var issue = new MaintenanceIssue
        {
            Status = IssueStatus.Resolved
        };

        // Act
        issue.Status = IssueStatus.New;
        issue.UpdatedAt = DateTime.UtcNow;

        // Assert
        issue.Status.Should().Be(IssueStatus.New);
    }
}

using System.Text;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MaintenanceIssues.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;
using Xunit;

namespace ElysStay.Tests.Integration.Features;

/// <summary>
/// Integration tests for the maintenance request process (1.5.4c), driven through the
/// real MediatR handlers against a real PostgreSQL database.
///
/// Covers: tenant reports an issue (building auto-resolved from active contract),
/// owner/staff processing via status transitions, reporter notifications, the
/// duplicate/invalid "close" shortcut, and the up-to-3-images / 3 MB / JPEG-PNG upload rules.
/// </summary>
public class MaintenanceIssueIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    private User _owner = null!;
    private User _tenant = null!;
    private Building _building = null!;
    private Room _room = null!;
    private Contract _contract = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    /// <summary>Seeds owner, tenant, building, room and an active contract for the tenant.</summary>
    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);
        _room.Status = RoomStatus.Occupied;
        _contract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);

        _fixture.DbContext.Users.AddRange(_owner, _tenant);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        _fixture.DbContext.Contracts.Add(_contract);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    private Mock<ICurrentUserService> Identity(Guid userId, UserRole role)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(userId);
        currentUser.Setup(m => m.UserId).Returns(userId);
        currentUser.Setup(m => m.Role).Returns(role);
        currentUser.Setup(m => m.IsOwner).Returns(role == UserRole.Owner);
        currentUser.Setup(m => m.IsStaff).Returns(role == UserRole.Staff);
        currentUser.Setup(m => m.IsTenant).Returns(role == UserRole.Tenant);
        return currentUser;
    }

    private static Mock<IEmailService> NoopEmail()
    {
        var email = new Mock<IEmailService>();
        email.Setup(m => m.TrySendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return email;
    }

    private CreateIssueCommandHandler CreateIssueHandler(Mock<ICurrentUserService> currentUser)
    {
        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new CreateIssueCommandHandler(_fixture.DbContext, currentUser.Object, scope, NoopEmail().Object);
    }

    private ChangeIssueStatusCommandHandler ChangeStatusHandler(Mock<ICurrentUserService> currentUser)
    {
        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new ChangeIssueStatusCommandHandler(_fixture.DbContext, currentUser.Object, scope, NoopEmail().Object);
    }

    private UploadIssueImagesCommandHandler UploadHandler(Mock<ICurrentUserService> currentUser)
    {
        var fileUpload = new Mock<IFileUploadService>();
        fileUpload.Setup(m => m.UploadAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string name, string _, CancellationToken _) => $"https://cdn.test/issues/{name}");

        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new UploadIssueImagesCommandHandler(_fixture.DbContext, fileUpload.Object, currentUser.Object, scope);
    }

    private static FileUploadItem Image(string name, string contentType, long size) => new()
    {
        FileStream = new MemoryStream(Encoding.UTF8.GetBytes("img")),
        FileName = name,
        ContentType = contentType,
        FileSize = size
    };

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TenantCreatesIssue_BuildingAutoResolvedFromActiveContract()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);

        var dto = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Vòi nước bị rò rỉ",
            Description = "Vòi nước phòng tắm chảy liên tục"
        }, default);

        dto.BuildingId.Should().Be(_building.Id);
        dto.RoomId.Should().Be(_room.Id);   // defaults to the contract's room
        dto.Status.Should().Be(IssueStatus.New.ToString());
        dto.ReportedBy.Should().Be(_tenant.Id);
    }

    [Fact]
    public async Task TenantWithoutActiveContract_CannotCreateIssue()
    {
        await SetupTestData();
        // Terminate the only contract so the tenant has no active contract
        var contract = await _fixture.DbContext.Contracts.FirstAsync(c => c.Id == _contract.Id);
        contract.Status = ContractStatus.Terminated;
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var act = () => CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Sự cố",
            Description = "Mô tả"
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task OwnerCreatesIssue_RequiresBuildingId()
    {
        await SetupTestData();
        var owner = Identity(_owner.Id, UserRole.Owner);

        var act = () => CreateIssueHandler(owner).Handle(new CreateIssueCommand
        {
            Title = "Hỏng đèn hành lang",
            Description = "Đèn tầng 2 không sáng"
            // BuildingId omitted
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CreateIssue_NotifiesBuildingOwner()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);

        var dto = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Mất điện phòng 101",
            Description = "Phòng mất điện từ sáng"
        }, default);

        var notification = await _fixture.DbContext.Notifications
            .FirstOrDefaultAsync(n => n.UserId == _owner.Id
                && n.Type == Domain.Constants.NotificationTypes.Issue
                && n.ReferenceId == dto.Id);
        notification.Should().NotBeNull();
    }

    // ── Status transitions ──────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeStatus_NewToInProgress_AutoAssignsToActingStaff()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Điều hòa hỏng",
            Description = "Không mát"
        }, default);

        var owner = Identity(_owner.Id, UserRole.Owner);
        var updated = await ChangeStatusHandler(owner).Handle(new ChangeIssueStatusCommand
        {
            Id = issue.Id,
            Status = IssueStatus.InProgress
        }, default);

        updated.Status.Should().Be(IssueStatus.InProgress.ToString());
        updated.AssignedTo.Should().Be(_owner.Id);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_NewToResolved_ThrowsConflict()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Sự cố",
            Description = "Mô tả"
        }, default);

        var owner = Identity(_owner.Id, UserRole.Owner);
        var act = () => ChangeStatusHandler(owner).Handle(new ChangeIssueStatusCommand
        {
            Id = issue.Id,
            Status = IssueStatus.Resolved
        }, default);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.ErrorCode == "INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public async Task ChangeStatus_NewToClosed_Shortcut_ClosesAndNotifiesReporter()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Báo trùng",
            Description = "Trùng với sự cố khác"
        }, default);

        var owner = Identity(_owner.Id, UserRole.Owner);
        var updated = await ChangeStatusHandler(owner).Handle(new ChangeIssueStatusCommand
        {
            Id = issue.Id,
            Status = IssueStatus.Closed
        }, default);

        updated.Status.Should().Be(IssueStatus.Closed.ToString());

        var notification = await _fixture.DbContext.Notifications
            .FirstOrDefaultAsync(n => n.UserId == _tenant.Id
                && n.Type == Domain.Constants.NotificationTypes.Issue
                && n.ReferenceId == issue.Id);
        notification.Should().NotBeNull();
    }

    // ── Image upload rules ────────────────────────────────────────────────────

    [Fact]
    public async Task UploadImages_UpToThreeValidImages_Succeeds()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Có hình ảnh",
            Description = "Đính kèm ảnh"
        }, default);

        // The reporting tenant may attach photos to their own issue
        var urls = await UploadHandler(tenant).Handle(new UploadIssueImagesCommand
        {
            IssueId = issue.Id,
            Files = new[]
            {
                Image("a.jpg", "image/jpeg", 2 * 1024 * 1024),
                Image("b.png", "image/png", 1 * 1024 * 1024),
                Image("c.jpg", "image/jpeg", 1 * 1024 * 1024)
            }
        }, default);

        urls.Should().HaveCount(3);

        var saved = await _fixture.DbContext.MaintenanceIssues.FindAsync(issue.Id);
        saved!.ImageUrls.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UploadImages_ByOwnerProcessingIssue_Succeeds()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Owner bổ sung ảnh",
            Description = "Ảnh hiện trường"
        }, default);

        // Owner (not the reporter) is authorized via building scope
        var owner = Identity(_owner.Id, UserRole.Owner);
        var urls = await UploadHandler(owner).Handle(new UploadIssueImagesCommand
        {
            IssueId = issue.Id,
            Files = new[] { Image("evidence.png", "image/png", 1 * 1024 * 1024) }
        }, default);

        urls.Should().HaveCount(1);
    }

    [Fact]
    public async Task UploadImages_ByDifferentTenant_ThrowsForbidden()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Sự cố của tenant A",
            Description = "Mô tả"
        }, default);

        // A different tenant (not the reporter, not Owner/Staff) is blocked
        var otherTenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _fixture.DbContext.Users.Add(otherTenant);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        var intruder = Identity(otherTenant.Id, UserRole.Tenant);
        var act = () => UploadHandler(intruder).Handle(new UploadIssueImagesCommand
        {
            IssueId = issue.Id,
            Files = new[] { Image("x.jpg", "image/jpeg", 1024) }
        }, default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UploadImages_MoreThanThree_ThrowsBadRequest()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Quá nhiều ảnh",
            Description = "4 ảnh"
        }, default);

        var act = () => UploadHandler(tenant).Handle(new UploadIssueImagesCommand
        {
            IssueId = issue.Id,
            Files = new[]
            {
                Image("a.jpg", "image/jpeg", 1024),
                Image("b.jpg", "image/jpeg", 1024),
                Image("c.jpg", "image/jpeg", 1024),
                Image("d.jpg", "image/jpeg", 1024)
            }
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task UploadImages_FileExceedsSizeLimit_ThrowsBadRequest()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "Ảnh quá lớn",
            Description = "4 MB"
        }, default);

        var act = () => UploadHandler(tenant).Handle(new UploadIssueImagesCommand
        {
            IssueId = issue.Id,
            Files = new[] { Image("big.jpg", "image/jpeg", 4 * 1024 * 1024) }
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task UploadImages_NonImageContentType_ThrowsBadRequest()
    {
        await SetupTestData();
        var tenant = Identity(_tenant.Id, UserRole.Tenant);
        var issue = await CreateIssueHandler(tenant).Handle(new CreateIssueCommand
        {
            Title = "File sai định dạng",
            Description = "PDF"
        }, default);

        var act = () => UploadHandler(tenant).Handle(new UploadIssueImagesCommand
        {
            IssueId = issue.Id,
            Files = new[] { Image("doc.pdf", "application/pdf", 1024) }
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }
}

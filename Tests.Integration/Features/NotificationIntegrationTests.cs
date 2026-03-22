using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class NotificationIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _user = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _user = TestDataBuilder.CreateUser();
        await _fixture.DbContext.Users.AddAsync(_user);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateNotification_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Invoice Ready",
            Message = "Your monthly invoice is ready",
            IsRead = false,
            Type = "Invoice",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _fixture.DbContext.Set<Notification>().AddAsync(notification);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Set<Notification>()
            .FirstOrDefault(n => n.Id == notification.Id);
        saved.Should().NotBeNull();
        saved!.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task MarkNotification_AsRead_UpdatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Test",
            Message = "Test message",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _fixture.DbContext.Set<Notification>().AddAsync(notification);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        notification.IsRead = true;
        _fixture.DbContext.Set<Notification>().Update(notification);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Set<Notification>()
            .FirstOrDefault(n => n.Id == notification.Id);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserNotifications_FiltersByUserId_ReturnsOnlyUserNotifications()
    {
        // Arrange
        await SetupTestData();
        var user2 = TestDataBuilder.CreateUser();
        await _fixture.DbContext.Users.AddAsync(user2);
        await _fixture.DbContext.SaveChangesAsync();

        var notif1 = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Test1",
            Message = "Message 1",
            CreatedAt = DateTime.UtcNow
        };

        var notif2 = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user2.Id,
            Title = "Test2",
            Message = "Message 2",
            CreatedAt = DateTime.UtcNow
        };

        await _fixture.DbContext.Set<Notification>().AddAsync(notif1);
        await _fixture.DbContext.Set<Notification>().AddAsync(notif2);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var userNotifications = _fixture.DbContext.Set<Notification>()
            .Where(n => n.UserId == _user.Id)
            .ToList();

        // Assert
        userNotifications.Should().HaveCount(1);
        userNotifications.First().UserId.Should().Be(_user.Id);
    }

    [Fact]
    public async Task GetUnreadNotifications_FiltersByIsRead_ReturnsOnlyUnreadNotifications()
    {
        // Arrange
        await SetupTestData();
        var unread = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Unread",
            Message = "Unread message",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        var read = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Read",
            Message = "Read message",
            IsRead = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        await _fixture.DbContext.Set<Notification>().AddAsync(unread);
        await _fixture.DbContext.Set<Notification>().AddAsync(read);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var unreadNotifications = _fixture.DbContext.Set<Notification>()
            .Where(n => n.UserId == _user.Id && !n.IsRead)
            .ToList();

        // Assert
        unreadNotifications.Should().HaveCount(1);
        unreadNotifications.First().IsRead.Should().BeFalse();
    }
}

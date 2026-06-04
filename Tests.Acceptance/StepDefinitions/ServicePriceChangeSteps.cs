using Application.Common.Interfaces;
using Application.Features.Services.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TechTalk.SpecFlow;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

/// <summary>
/// Step definitions for ServicePriceChange.feature.
/// Drives the real UpdateServiceCommandHandler against a live PostgreSQL container,
/// verifying PR-03 price-change tracking (PreviousUnitPrice / PriceUpdatedAt).
/// Scoped to "Service Price Change" so its bindings don't collide with other features.
/// </summary>
[Binding]
[Scope(Feature = "Service Price Change")]
public class ServicePriceChangeSteps
{
    private readonly DatabaseFixture _fixture;

    private User _owner = null!;
    private Building _building = null!;
    private Service _service = null!;

    private decimal? _previousBefore;
    private DateTime? _priceUpdatedAtBefore;
    private Exception? _lastException;

    public ServicePriceChangeSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private UpdateServiceCommandHandler CreateHandler()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(_owner.Id);
        currentUser.Setup(m => m.UserId).Returns(_owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);
        currentUser.Setup(m => m.Role).Returns(UserRole.Owner);
        return new UpdateServiceCommandHandler(_fixture.DbContext, currentUser.Object);
    }

    private async Task<Service> ReloadService()
        => await _fixture.DbContext.Services.AsNoTracking().FirstAsync(s => s.Id == _service.Id);

    // ── Background ─────────────────────────────────────────────────────────────

    [Given("a building owner with email \"([^\"]*)\"")]
    public async Task GivenABuildingOwnerWithEmail(string email)
    {
        _owner = TestDataBuilder.CreateUser(email: email, role: UserRole.Owner);
        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a building named \"([^\"]*)\" owned by the owner")]
    public async Task GivenABuildingNamedOwnedByTheOwner(string name)
    {
        _building = TestDataBuilder.CreateBuilding(_owner.Id, name: name);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a service \"([^\"]*)\" exists with current unit price ([0-9]+) VND")]
    public async Task GivenAServiceExistsWithCurrentUnitPrice(string name, decimal unitPrice)
    {
        _service = TestDataBuilder.CreateService(_building.Id, name: name, unitPrice: unitPrice);
        // Stamp an older timestamp so a later price change produces a clearly newer value.
        _service.PriceUpdatedAt = DateTime.UtcNow.AddDays(-1);
        await _fixture.DbContext.Services.AddAsync(_service);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── Given — initial price state ─────────────────────────────────────────────

    [Given("the service has no previous unit price")]
    public async Task GivenTheServiceHasNoPreviousUnitPrice()
        => await UpdateServiceState(previousUnitPrice: null, clearPrevious: true);

    [Given("current unit price is ([0-9]+) VND")]
    public async Task GivenCurrentUnitPriceIs(decimal price)
        => await UpdateServiceState(unitPrice: price);

    [Given("previous unit price is ([0-9]+) VND")]
    public async Task GivenPreviousUnitPriceIs(decimal price)
        => await UpdateServiceState(previousUnitPrice: price);

    private async Task UpdateServiceState(
        decimal? unitPrice = null, decimal? previousUnitPrice = null, bool clearPrevious = false)
    {
        var service = await _fixture.DbContext.Services.FirstAsync(s => s.Id == _service.Id);
        if (unitPrice.HasValue) service.UnitPrice = unitPrice.Value;
        if (clearPrevious) service.PreviousUnitPrice = null;
        else if (previousUnitPrice.HasValue) service.PreviousUnitPrice = previousUnitPrice.Value;
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── When ───────────────────────────────────────────────────────────────────

    [When("the owner updates service price to (-?[0-9]+) VND")]
    public async Task WhenOwnerUpdatesServicePriceTo(decimal newPrice)
    {
        var before = await ReloadService();
        _previousBefore = before.PreviousUnitPrice;
        _priceUpdatedAtBefore = before.PriceUpdatedAt;

        try
        {
            await CreateHandler().Handle(
                new UpdateServiceCommand { Id = _service.Id, UnitPrice = newPrice }, default);
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    // ── Then ───────────────────────────────────────────────────────────────────

    [Then("current unit price should be ([0-9]+) VND")]
    [Then("current unit price should remain ([0-9]+) VND")]
    public async Task ThenCurrentUnitPriceShouldBe(decimal expected)
        => (await ReloadService()).UnitPrice.Should().Be(expected);

    [Then("previous unit price should be ([0-9]+) VND")]
    public async Task ThenPreviousUnitPriceShouldBe(decimal expected)
        => (await ReloadService()).PreviousUnitPrice.Should().Be(expected);

    [Then("previous unit price should remain unchanged")]
    public async Task ThenPreviousUnitPriceShouldRemainUnchanged()
        => (await ReloadService()).PreviousUnitPrice.Should().Be(_previousBefore);

    [Then("price updated timestamp should be changed")]
    public async Task ThenPriceUpdatedTimestampShouldBeChanged()
        => (await ReloadService()).PriceUpdatedAt.Should().BeAfter(_priceUpdatedAtBefore!.Value);

    [Then("price updated timestamp should remain unchanged")]
    public async Task ThenPriceUpdatedTimestampShouldRemainUnchanged()
        => (await ReloadService()).PriceUpdatedAt.Should().Be(_priceUpdatedAtBefore);

    [Then("validation error should be displayed")]
    public void ThenValidationErrorShouldBeDisplayed()
        => _lastException.Should().NotBeNull("an invalid price should have been rejected");
}

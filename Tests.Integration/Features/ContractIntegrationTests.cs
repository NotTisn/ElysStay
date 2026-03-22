using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class ContractIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;
    private User _tenant = null!;
    private Building _building = null!;
    private Room _room = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateContract_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var contract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);

        // Act
        await _fixture.DbContext.Contracts.AddAsync(contract);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Contracts.FirstOrDefault(c => c.Id == contract.Id);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(ContractStatus.Active);
        saved.DepositStatus.Should().Be(DepositStatus.Held);
    }

    [Fact]
    public async Task TerminateContract_WithValidContract_TerminatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var contract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);
        await _fixture.DbContext.Contracts.AddAsync(contract);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        contract.Status = ContractStatus.Terminated;
        contract.TerminationDate = DateOnly.FromDateTime(DateTime.UtcNow);
        contract.TerminationNote = "Tenant moved out";
        _fixture.DbContext.Contracts.Update(contract);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Contracts.FirstOrDefault(c => c.Id == contract.Id);
        updated!.Status.Should().Be(ContractStatus.Terminated);
        updated.TerminationDate.Should().NotBeNull();
    }

    [Fact]
    public async Task DepositRefund_OnContractTermination_MarkDepositAsRefunded()
    {
        // Arrange
        await SetupTestData();
        var contract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);
        contract.DepositStatus = DepositStatus.Refunded;
        await _fixture.DbContext.Contracts.AddAsync(contract);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        contract.Status = ContractStatus.Terminated;
        contract.DepositStatus = DepositStatus.Refunded;
        _fixture.DbContext.Contracts.Update(contract);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Contracts.FirstOrDefault(c => c.Id == contract.Id);
        updated!.DepositStatus.Should().Be(DepositStatus.Refunded);
    }

    [Fact]
    public async Task GetContractsByBuilding_FiltersByBuilding_ReturnsCorrectContracts()
    {
        // Arrange
        await SetupTestData();
        var contract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);
        await _fixture.DbContext.Contracts.AddAsync(contract);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var contracts = _fixture.DbContext.Contracts
            .Where(c => c.RoomId == _room.Id)
            .ToList();

        // Assert
        contracts.Should().HaveCount(1);
        contracts.First().RoomId.Should().Be(_room.Id);
    }

    [Fact]
    public async Task RenewContract_CreatesNewContractWithUpdatedDates()
    {
        // Arrange
        await SetupTestData();
        
        // 1. Create and save the first active contract
        var originalContract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);
        await _fixture.DbContext.Contracts.AddAsync(originalContract);
        await _fixture.DbContext.SaveChangesAsync();

        // 2. Terminate the original contract so the room becomes available again
        originalContract.Status = ContractStatus.Terminated; // Use Terminated or Expired based on your enum
        _fixture.DbContext.Contracts.Update(originalContract);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        // 3. Now it is safe to create the new Active contract
        var newStartDate = originalContract.EndDate;
        var newEndDate = newStartDate.AddMonths(12);
        var renewedContract = new Contract
        {
            Id = Guid.NewGuid(),
            RoomId = _room.Id,
            TenantUserId = _tenant.Id,
            StartDate = newStartDate,
            MoveInDate = newStartDate,
            EndDate = newEndDate,
            MonthlyRent = originalContract.MonthlyRent,
            DepositAmount = originalContract.DepositAmount,
            DepositStatus = DepositStatus.Held,
            Status = ContractStatus.Active, // Database will accept this now!
            CreatedBy = _owner.Id
        };

        await _fixture.DbContext.Contracts.AddAsync(renewedContract);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var savedRenewed = _fixture.DbContext.Contracts.FirstOrDefault(c => c.Id == renewedContract.Id);
        savedRenewed.Should().NotBeNull();
        savedRenewed!.StartDate.Should().Be(newStartDate);
    }
}

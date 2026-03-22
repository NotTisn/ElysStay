# ElysStay Testing Guide

Comprehensive testing documentation for the ElysStay rental management system. This guide covers test architecture, execution, and best practices.

## Quick Start

### Prerequisites
- .NET 10.0 SDK
- PostgreSQL 15+ (for integration tests, managed by Testcontainers)
- Docker (for Testcontainers)

### Setup & Run All Tests

```powershell
# Restore NuGet packages
dotnet restore

# Run all tests
dotnet test

# Run all tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage" --logger "html" --results-directory "./test-results"
```

---

## Test Project Structure

### 1. **Tests.Acceptance** (Zone A - BDD)
Behavior-driven development tests using Gherkin/Cucumber feature files with SpecFlow step definitions.

**Location:** `Tests.Acceptance/`

**Projects & Files:**
- `Features/` — Feature files defining business scenarios
- `StepDefinitions/` — SpecFlow step implementations

**Coverage:**
- `InvoiceCalculation.feature` — Monthly invoice calculation with room + service charges
- `DoubleBookingPrevention.feature` — Prevent overlapping room reservations
- `MeterReadingBilling.feature` — Utility consumption & billing calculations
- `ContractLifecycle.feature` — Contract active/terminated & deposit refund flow
- `ReservationConversion.feature` — Convert reservations to contracts
- `PaymentTracking.feature` — Payment recording & status transitions

**Key Test Data Builders:**
- `TestDataBuilder.CreateUser()` — Creates test users with roles
- `TestDataBuilder.CreateBuilding()` — Creates buildings with owner
- `TestDataBuilder.CreateRoom()` — Creates rooms with pricing
- `TestDataBuilder.CreateContract()` — Creates contracts with terms
- `TestDataBuilder.CreateInvoice()` — Creates invoices with amounts

**Run Zone A Tests:**
```powershell
dotnet test Tests.Acceptance --logger "console;verbosity=minimal"
```

---

### 2. **Tests.Integration** (Zone B - High-Impact Endpoints)
Integration tests validating endpoint behavior with real database interactions using Testcontainers.

**Location:** `Tests.Integration/`

**Test Classes (50+ tests):**

| Class | Purpose | Key Tests |
|-------|---------|-----------|
| `InvoiceIntegrationTests` | Invoice CRUD & calculations | Generate, update status, void, filter |
| `PaymentIntegrationTests` | Payment recording & tracking | Record, partial, multiple, methods |
| `ContractIntegrationTests` | Contract lifecycle | Create, terminate, deposit refund, renew |
| `ReservationIntegrationTests` | Reservation management | Create, convert to contract, cancel |
| `RoomIntegrationTests` | Room management | Create, update price/status, soft delete, filter |
| `MeterReadingIntegrationTests` | Meter readings & consumption | Record, calculate, filter, prevent duplicates |
| `BuildingIntegrationTests` | Building operations | Create, update, filter by owner, assign staff |
| `ExpenseIntegrationTests` | Expense tracking | Create, filter by building/category |
| `NotificationIntegrationTests` | User notifications | Create, mark read, filter by user/unread |

**Infrastructure:**
- `Fixtures/DatabaseFixture.cs` — PostgreSQL Testcontainers fixture for clean database per test
- `Builders/TestDataBuilder.cs` — Factory methods for consistent test entity creation

**Run Zone B Tests:**
```powershell
dotnet test Tests.Integration --logger "console;verbosity=minimal"

# With coverage
dotnet test Tests.Integration --collect:"XPlat Code Coverage"
```

---

### 3. **Tests.Unit** (Zone C - Business Logic Validation)
Unit tests for data validation, enum conversions, and business rule enforcement (no database).

**Location:** `Tests.Unit/Business/`

**Test Classes (30+ tests):**

| Class | Purpose | Key Tests |
|-------|---------|-----------|
| `InvoiceCalculationUnitTests` | Invoice amount calculations | Total formula, service charges, discounts, penalties |
| `DepositStatusTransitionUnitTests` | Deposit status flow | Unpaid → PartiallyPaid → Paid → Refunded |
| `ContractStatusValidationUnitTests` | Contract status rules | Active/Terminated transitions |
| `EntityValidationUnitTests` | Required fields & constraints | Email validity, foreign keys, amounts > 0 |
| `EnumConversionUnitTests` | Enum serialization | String ↔ Enum conversions for all enum types |
| `PaymentMethodValidationUnitTests` | Payment method validation | Cash, BankTransfer, Momo, ZaloPay support |
| `RoomAvailabilityUnitTests` | Room status rules | Available for reservation, Occupied/Maintenance/Deleted cannot be reserved |

**Run Zone C Tests:**
```powershell
dotnet test Tests.Unit --logger "console;verbosity=minimal"

# With coverage
dotnet test Tests.Unit --collect:"XPlat Code Coverage"
```

---

## Test Categorization (Decision-Support Model)

### Zone A: Cucumber BDD (High Business Impact + High Complexity)
**When:** Complex business rules needing stakeholder communication
**Features:** 6-8 `.feature` files with Gherkin scenarios
**Examples:** Invoice calculation, double-booking prevention, contract lifecycle
**Run Command:** `dotnet test Tests.Acceptance`

### Zone B: xUnit Integration (High Business Impact + Low Complexity)
**When:** Endpoint/API validation with database state
**Tests:** 50+ tests covering all major CRUD operations
**Examples:** Payment recording, room management, meter readings
**Infrastructure:** `DatabaseFixture` (PostgreSQL Testcontainers) + `TestDataBuilder`
**Run Command:** `dotnet test Tests.Integration`

### Zone C: xUnit Unit (Low Business Impact)
**When:** Data validation, enum conversions, simple business logic
**Tests:** 30+ tests for rules enforcement
**Examples:** Invoice totals, status transitions, field validation
**Run Command:** `dotnet test Tests.Unit`

---

## Running Specific Tests

### Run Single Test Class
```powershell
dotnet test Tests.Integration --filter "InvoiceIntegrationTests"
```

### Run Single Test Method
```powershell
dotnet test Tests.Integration --filter "InvoiceIntegrationTests.GenerateInvoice_WithValidContract_CreatesInvoiceSuccessfully"
```

### Run Tests by Category
```powershell
# All acceptance tests
dotnet test Tests.Acceptance

# All integration tests
dotnet test Tests.Integration

# All unit tests
dotnet test Tests.Unit
```

### Run with Specific Logger
```powershell
# Console output
dotnet test --logger "console;verbosity=detailed"

# HTML report
dotnet test --logger "html" --results-directory "./test-results"

# xUnit (default)
dotnet test --logger "xunit" --results-directory "./test-results"
```

---

## Code Coverage

### Generate Coverage Report

```powershell
# Run all tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --results-directory "./coverage"

# Install ReportGenerator (one-time)
dotnet tool install -g reportgenerator

# Generate HTML report
reportgenerator -reports:"./coverage/**/coverage.cobertura.xml" -targetdir:"./coverage_report" -reporttypes:"Html"

# View report
start ./coverage_report/index.html
```

### Coverage Targets
- **Business Logic (Zone C):** >85% coverage
- **Integration Tests (Zone B):** >70% coverage
- **Overall Project:** >70% coverage

### Viewing Coverage in VS Code
1. Install "Coverage Gutters" extension (ryanluker.vscode-coverage-gutters)
2. Run tests with coverage
3. Open `coverage.cobertura.xml` in VS Code
4. Coverage will highlight in editor

---

## Test Data Setup

### Using TestDataBuilder

```csharp
// Create user with specific role
var owner = TestDataBuilder.CreateUser(
    email: "owner@test.com",
    role: UserRole.Manager);

// Create building owned by user
var building = TestDataBuilder.CreateBuilding(
    owner.Id,
    name: "Apartment Complex");

// Create room with custom pricing
var room = TestDataBuilder.CreateRoom(
    building.Id,
    roomNumber: "101",
    price: 5_000_000);

// Create contract
var contract = TestDataBuilder.CreateContract(
    room.Id,
    tenant.Id,
    owner.Id,
    roomPrice: 5_000_000,
    depositAmount: 10_000_000);

// Create invoice
var invoice = TestDataBuilder.CreateInvoice(
    contract.Id,
    owner.Id,
    billingMonth: 3,
    billingYear: 2026);

// Create payment
var payment = TestDataBuilder.CreatePayment(
    invoice.Id,
    owner.Id,
    amount: 5_000_000,
    method: PaymentMethod.BankTransfer);
```

### Using DatabaseFixture in Integration Tests

```csharp
public class SampleIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    public async Task InitializeAsync()
        => await _fixture.InitializeAsync();  // Start PostgreSQL container

    public async Task DisposeAsync()
        => await _fixture.DisposeAsync();     // Clean up container

    [Fact]
    public async Task MyTest()
    {
        // Arrange - test data persisted to real database
        await _fixture.DbContext.SaveChangesAsync();
        
        // Act & Assert - test against real database state
    }
}
```

---

## Continuous Integration

### GitHub Actions Workflow

Create `.github/workflows/tests.yml`:

```yaml
name: Run Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: password
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Run tests
        run: dotnet test --collect:"XPlat Code Coverage" --logger "html"
      
      - name: Upload coverage
        uses: codecov/codecov-action@v3
        with:
          files: ./coverage/**/coverage.cobertura.xml
```

### Local Pre-Commit Testing

```powershell
# Run all tests before committing
dotnet test --logger "console"

# Only run tests, no build
dotnet test --no-build

# Run fast tests only (skip integration tests)
dotnet test Tests.Unit
```

---

## Best Practices

### Writing Tests

1. **AAA Pattern** (Arrange-Act-Assert)
   ```csharp
   [Fact]
   public async Task MyTest()
   {
       // Arrange - set up test data
       var entity = TestDataBuilder.CreateUser();
       
       // Act - execute business logic
       var result = await DoSomething(entity);
       
       // Assert - verify expected outcome
       result.Should().NotBeNull();
   }
   ```

2. **Use Descriptive Test Names**
   - Format: `MethodName_Scenario_ExpectedBehavior`
   - Example: `CreateInvoice_WithValidContract_CreatesSuccessfully`

3. **One Assertion Per Test** (Ideally)
   - If multiple assertions, group related ones
   - Use `FluentAssertions` for readability

4. **Use Test Builders**
   - Don't hardcode test data
   - Use `TestDataBuilder` for consistency
   - Override specific properties as needed

5. **Clean Database Between Tests**
   - `DatabaseFixture.ResetAsync()` clears test database
   - Ensures test isolation

### Avoiding Common Mistakes

1. ❌ **Don't** test multiple unrelated scenarios in one test
   - ✅ Create separate tests for each scenario

2. ❌ **Don't** use Thread.Sleep() for async waiting
   - ✅ Use async/await properly

3. ❌ **Don't** share test data between tests
   - ✅ Each test creates its own data

4. ❌ **Don't** test implementation details
   - ✅ Test behavior and outcomes

5. ❌ **Don't** mock database in integration tests
   - ✅ Use real `DatabaseFixture` with Testcontainers

---

## Troubleshooting

### Docker Not Available
**Error:** "Cannot connect to Docker daemon"
**Solution:** 
- Ensure Docker Desktop is running
- Or install Docker on Linux systems
- For CI/CD: Docker comes pre-installed on GitHub Actions

### Tests Hang or Timeout
**Error:** "Test timed out"
**Solution:**
- Check if PostgreSQL container is starting properly
- Increase test timeout: `--configuration Release` runs faster
- Check `DatabaseFixture` logs

### Test Data Not Persisting
**Error:** "Entity not found" in second test
**Solution:**
- Call `await _fixture.DbContext.SaveChangesAsync()`
- Verify entity was added before saving
- Check `DatabaseFixture.ResetAsync()` isn't clearing data prematurely

### SQL Errors in Integration Tests
**Error:** "Column X does not exist" or migration errors
**Solution:**
- Run `dotnet ef migrations add <name> -p Infrastructure`
- Run `dotnet ef database update`
- Ensure migrations are applied: `DatabaseFixture.MigrateAsync()`

---

## Test Coverage by Feature

| Feature | Zone | Test Count | Status |
|---------|------|-----------|--------|
| Invoice Calculation | A, B, C | 8 | ✅ |
| Payment Recording | B, C | 7 | ✅ |
| Contract Lifecycle | A, B, C | 9 | ✅ |
| Reservation Management | A, B | 6 | ✅ |
| Room Management | B, C | 8 | ✅ |
| Meter Reading/Billing | A, B | 6 | ✅ |
| Building Operations | B | 4 | ✅ |
| Expense Tracking | B | 4 | ✅ |
| Notifications | B, C | 6 | ✅ |
| **Total** | | **58** | ✅ |

---

## Integration with CI/CD

### Push to Main Branch
1. GitHub Actions runs all tests
2. Code coverage report generated
3. Results available in Artifacts
4. Pull requests blocked if coverage drops

### Local Development
```powershell
# Quick test before pushing
dotnet test Tests.Unit && dotnet test Tests.Integration

# Full test with coverage before PR
dotnet test --collect:"XPlat Code Coverage"
```

---

## Contributing New Tests

When adding a new feature:

1. **Write Acceptance Test First (TDD)**
   - Create `.feature` file in `Tests.Acceptance/Features/`
   - Write Gherkin scenarios
   - Implement step definitions

2. **Add Integration Tests**
   - Create test class in `Tests.Integration/Features/`
   - Test all CRUD operations & business rules

3. **Add Unit Tests** (if needed)
   - Create test class in `Tests.Unit/Business/`
   - Test data validation & calculations

4. **Update This README**
   - Add feature to test coverage table
   - Document any special setup requirements

---

## Resources

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [SpecFlow (Cucumber)](https://specflow.org/)
- [Testcontainers](https://testcontainers.com/)
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
- [Code Coverage Gutters](https://marketplace.visualstudio.com/items?itemName=ryanluker.vscode-coverage-gutters)

---

## Support

For test-related questions:
- Check test examples in `Tests.Integration/Features/`
- Review `TestDataBuilder` for entity creation patterns
- Run specific test with `--logger "console;verbosity=detailed"` for debugging

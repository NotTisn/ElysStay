# Quick Reference - ElysStay Testing

## 🚀 Quick Start (30 seconds)

```powershell
# Windows
cd c:\Users\LENOVO\IdeaProjects\ElysStay
.\run-tests.bat

# Linux/Mac
cd /path/to/ElysStay
./run-tests.sh
```

## 📊 Test Suite at a Glance

| Zone | Type | Tests | Command |
|------|------|-------|---------|
| **A** | Cucumber BDD | 21 | `.\run-tests.bat acceptance` |
| **B** | Integration | 47 | `.\run-tests.bat integration` |
| **C** | Unit | 47 | `.\run-tests.bat unit` |
| **ALL** | Combined | 115 | `.\run-tests.bat` |

## 🔍 Common Commands

```powershell
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "InvoiceIntegrationTests"

# Run single test method
dotnet test --filter "GenerateInvoice_WithValidContract_CreatesInvoiceSuccessfully"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML coverage
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"./coverage_report" -reporttypes:"Html"
```

## 📁 Key Files

| File | Purpose |
|------|---------|
| `TESTING.md` | Full testing guide (read this!) |
| `TEST_SUMMARY.md` | Overview of all tests |
| `TEST_INVENTORY.md` | Complete inventory with counts |
| `run-tests.bat` | Windows test runner |
| `run-tests.sh` | Linux/Mac test runner |

## 🧪 Test Locations

```
Tests.Acceptance/        → Cucumber BDD features
  ├── Features/          → 6 .feature files
  └── StepDefinitions/   → 6 step definition files

Tests.Integration/       → Real database tests
  ├── Features/          → 9 test classes (47 tests)
  ├── Fixtures/          → DatabaseFixture
  └── Builders/          → TestDataBuilder

Tests.Unit/              → Business logic tests
  └── Business/          → 7 test classes (47 tests)
```

## 🔧 Test Data Creation

```csharp
using ElysStay.Tests.Integration.Builders;

// Create users
var owner = TestDataBuilder.CreateUser(role: UserRole.Manager);
var tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);

// Create entities
var building = TestDataBuilder.CreateBuilding(owner.Id);
var room = TestDataBuilder.CreateRoom(building.Id);
var contract = TestDataBuilder.CreateContract(room.Id, tenant.Id, owner.Id);
var invoice = TestDataBuilder.CreateInvoice(contract.Id, owner.Id);
var payment = TestDataBuilder.CreatePayment(invoice.Id, owner.Id, amount);
```

## 💡 Test Pattern

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange - set up test data
    var entity = TestDataBuilder.CreateUser();
    await _fixture.DbContext.SaveChangesAsync();
    
    // Act - execute logic
    var result = await DoSomething(entity);
    
    // Assert - verify outcome
    result.Should().NotBeNull();
    result.Status.Should().Be(ExpectedStatus);
}
```

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| Docker not found | Start Docker Desktop or install Docker |
| Test timeout | Run in Release mode: `dotnet test -c Release` |
| NuGet errors | Clear cache: `dotnet nuget locals all --clear` then restore |
| Migration errors | Run: `dotnet ef database update -p Infrastructure -s API` |

## 📈 Coverage Goals

- **Overall:** >70%
- **Business Logic (Zone C):** >85%
- **Integration (Zone B):** >70%
- **Cucumber Scenarios:** 100%

## 🎯 Features Tested

✅ Invoice calculation (room + services + penalties/discounts)
✅ Payment recording (Cash, BankTransfer, Momo, ZaloPay)
✅ Contract lifecycle (Active → Terminated)
✅ Reservation management (Create, convert, cancel)
✅ Room management (Create, update, soft delete)
✅ Meter reading & utility billing
✅ Building operations
✅ Expense tracking
✅ Notifications
✅ Data validation & enum conversions

## 📞 Support

- Check `TESTING.md` for detailed guide
- Review test examples in `Tests.Integration/Features/`
- Look at `TestDataBuilder` for entity patterns
- Run tests with `--logger "console;verbosity=detailed"` for debugging

---

**115+ Tests Ready to Execute! 🚀**

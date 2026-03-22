# Integration Tests - Fixing Guide

## Status
68 compilation errors remaining. All related to entity model mismatches introduced during auto-generation.

## Root Causes

### 1. Entity Property Name Mismatches
The generated tests assume property names that don't exist in the actual entities:

```
❌ INCORRECT          ✅ CORRECT
Invoice.RoomAmount → Invoice.RentAmount
Contract.RoomPrice → ??? (need to check Contract entity)
RoomReservation.CancelReason → ??? (need to check)
```

### 2. Enum Value Mismatches
```
❌ INCORRECT          ✅ CORRECT
UserRole.Manager → UserRole.Owner (or Staff)
InvoiceStatus.Unpaid → InvoiceStatus.Draft  
InvoiceStatus.PartialPaid → InvoiceStatus.PartiallyPaid
InvoiceStatus.Cancelled → InvoiceStatus.Void
DepositStatus.Unpaid → DepositStatus.Held
DepositStatus.Paid → DepositStatus.Refunded
RoomStatus.Deleted → ??? (doesn't exist)
```

### 3. Type Mismatches
```
DateTime → DateOnly  (for date-only fields)
Payment.Method (string) vs Payment.PaymentMethod (string)
```

## Fixing Strategy

### Step 1: Verify Actual Entity Definitions
Check these files and document the ACTUAL property names and types:
- `Domain/Entities/Contract.cs` - Check: RoomPrice? StartDate type?
- `Domain/Entities/RoomReservation.cs` - Check: CancelReason? RefundDate?
- `Domain/Entities/BuildingStaff.cs` - Check: spelling/existence

### Step 2: Update TestDataBuilder
Fix the builder methods in `Tests.Integration/Builders/TestDataBuilder.cs`:

```csharp
// Current (WRONG):
public static Invoice CreateInvoice(
    decimal roomAmount = 5_000_000,
    InvoiceStatus status = InvoiceStatus.Unpaid)

// Should be:
public static Invoice CreateInvoice(
    decimal rentAmount = 5_000_000,
    InvoiceStatus status = InvoiceStatus.Draft)
```

### Step 3: Fix All Test Files
For each of 9 test files, update:
- Enum values used
- Property assignments
- DateTime → DateOnly conversions
- UserRole references (Manager → Owner/Staff)

### Specific Fixes Needed (from error messages):

#### In TestDataBuilder.cs (Builders folder):
- Line 21: User.PasswordHash - check if it exists
- Line 40: Building.TotalRooms - check actual name
- Line 98-103: Contract fields (RoomPrice, dates)
- Line 100: DepositStatus.Unpaid → Held
- Line 102-103: DateTime dates → DateOnly

#### In InvoiceIntegrationTests.cs:
- Line 24: UserRole.Manager → Owner
- Line 49-51: Invoice.RoomAmount → RentAmount, status to Draft, DateTime → DateOnly
- Line 109, 128: InvoiceStatus.PartialPaid → PartiallyPaid, Cancelled → Void

#### In PaymentIntegrationTests.cs:
- Line 23: UserRole.Manager → Owner
- Line 44, 54: Invoice.RoomAmount → RentAmount
- Line 53: Payment.Status → doesn't exist (remove?)
- Line 106: PaymentMethod references → use string values

#### In RoomIntegrationTests.cs:
- Line 21: UserRole.Manager → Owner
- Line 71, 95, 102: RoomStatus.Deleted → doesn't exist (remove or use different status)

#### In All Test Files:
- DateOnly conversion issues (DateTime.UtcNow → DateOnly.FromDateTime(...))
- BuildingStaff references - check if correct entity name

## Command to Check Errors

```bash
# See all compilation errors grouped
dotnet test Tests.Integration 2>&1 | grep "error CS"

# Count by error type
dotnet test Tests.Integration 2>&1 | grep "error CS" | cut -d: -f4 | sort | uniq -c | sort -rn
```

## Quick Fix Order (Highest Priority First)

1. ✅ Fix namespace imports (already done)
2. 🔄 Fix TestDataBuilder enum/property names
3. 🔄 Update all 9 test files to use correct enums  
4. 🔄 Fix DateTime → DateOnly conversions
5. 🔄 Remove references to non-existent entities/properties

## Expected Result After Fixes

```bash
$ dotnet test Tests.Integration
[xUnit.net ...] Starting: Tests.Integration
Test summary: total: 47, failed: 0, succeeded: 47, skipped: 0
```

## Questions to Answer First

Before fixing, get answers to:
1. What is the actual name of the property for room price on Contract?
2. Does RoomReservation have CancelReason and RefundDate fields?
3. Is it BuildingStaff or StaffAssignment?
4. What are the actual enum values for all enums (double-check)?
5. Which DateTime fields should be DateOnly?

These answers should come from reviewing the actual entity definitions in `Domain/Entities/`


# Rental Management System — API Design Specification

> **Version:** 3.0 — Clean  
> **Last verified:** 2026-03-01  
> **Status:** VISION — source of truth for design decisions  
> **Companion files:** `endpoints.csv` (work), `entities.csv` (data), `business-rules.csv` (rules)

This document covers architecture, patterns, algorithms, and decisions that don't fit in tabular format. It intentionally does NOT repeat endpoint tables, entity field lists, or individual business rules — those live in the CSV files. If something is here AND in a CSV, the CSV is wrong and must be fixed to match this document.

---

## §1 System Overview

**Product:** RESTful API for Vietnamese landlords managing rental properties (nhà trọ / phòng trọ).

**Core workflow:**
```
Owner creates Building → adds Rooms → adds Services (fees) →
Staff/Owner manages Tenants → creates Reservations → converts to Contracts →
Monthly: records MeterReadings → generates Invoices → records Payments →
Ongoing: handles Issues, tracks Expenses → views Reports/PnL
```

**Roles:**
| Role | Who | Access |
|------|-----|--------|
| OWNER | Landlord | Everything. Creates buildings, staff, tenants. Sees all data. |
| STAFF | Assigned employee | Scoped to assigned buildings. Cannot create buildings or staff. |
| TENANT | Renter | Own data only (contract, invoices, payments, issues). Auto-filtered. |

---

## §2 Entity Relationships

> Field-level detail is in `entities.csv`. This section shows how entities connect.

```
User (OWNER/STAFF/TENANT)
 ├── 1:N Building (as owner)
 │    ├── N:N User/Staff (via BuildingStaff)
 │    ├── 1:N Room
 │    │    ├── 1:N Reservation
 │    │    ├── 1:N Contract (max 1 ACTIVE per room)
 │    │    │    ├── N:N User/Tenant (via ContractTenant — roommates)
 │    │    │    └── 1:N Invoice (unique per contract+month)
 │    │    │         ├── 1:N InvoiceDetail (line items)
 │    │    │         └── 1:N Payment (partial payments allowed)
 │    │    ├── 1:N MeterReading (per room+service+month)
 │    │    └── N:N Service (via RoomService — per-room override)
 │    ├── 1:N Service (fee config: electricity, water, etc.)
 │    ├── 1:N Expense
 │    └── (Room → 0:N Expense, optional room-level tracking)
 ├── 1:1 TenantProfile (CCCD data, auto-created)
 ├── 1:N Issue (as reporter)
 └── 1:N Notification (as recipient)
```

**Key relationship rules:**
- Payment links to EITHER Invoice (rent) OR Contract (deposit) — never both.
- RoomService is an override layer. If absent → building Service defaults apply.
- ContractTenant tracks roommate history. MoveOutDate = soft remove (stays in records).

---

## §3 State Machines

### Room Status
```
                    ┌─────────────────────────────────────────┐
                    │                                         │
AVAILABLE ──(reservation)──→ BOOKED ──(contract)──→ OCCUPIED ─┘
    ↑                          │                       │   (terminate)
    │          (cancel/expire) │                       │
    │◄─────────────────────────┘                       │
    │◄─────────────────────────────────────────────────┘
    │
    ↕ (manual PATCH only)
MAINTENANCE
```

**AVAILABLE → OCCUPIED** also valid (direct contract, no reservation).  
**All writes go through `UpdateRoomStatusAsync(roomId, newStatus, expectedCurrentStatus)`** with EF Core RowVersion concurrency. No code path writes Room.Status directly.

### Reservation Status
```
PENDING ──(confirm)──→ CONFIRMED ──(contract created)──→ CONVERTED
   │                      │
   │──(cancel)──→ CANCELLED ←──(cancel)──┘
   │                      │
   └──(bg job: past ExpiresAt)──→ EXPIRED ←──(bg job: past ExpiresAt)──┘
```

Deposit enters Payment ledger only at resolution (CONVERTED, CANCELLED, EXPIRED) — not at creation. Both PENDING and CONFIRMED reservations can expire.

### Contract Status
```
ACTIVE ──(explicit terminate or renew)──→ TERMINATED
```

**There is no EXPIRED state.** Contracts past EndDate stay ACTIVE. The dashboard warns "N contracts past due for renewal." The owner acts — you don't auto-evict people.

### Invoice Status
```
DRAFT ──(send)──→ SENT ──(partial pay)──→ PARTIALLY_PAID ──(full pay)──→ PAID
                    │                                                      ↑
                    │──(full pay)──────────────────────────────────────────┘
                    │
                    └──(bg job: past DueDate)──→ OVERDUE ──(payment)──→ PAID or PARTIALLY_PAID

Any except PAID ──(void)──→ VOID
```

PaidAmount is **computed** (`SUM(Payment.Amount) WHERE InvoiceId`), never stored. Auto-transitions happen when payment is recorded: compare PaidAmount vs TotalAmount.

### Issue Status
```
NEW ──→ IN_PROGRESS ──→ RESOLVED ──→ CLOSED
 │
 └──→ CLOSED (invalid/duplicate — shortcut)
```

---

## §4 Architecture

```
┌──────────────────────────────────────────────┐
│              API Controllers                  │
│   [Authorize], route: /api/v1/...            │
├──────────────────────────────────────────────┤
│              Service Layer                    │
│   Interfaces + implementations. All logic.   │
├──────────────────────────────────────────────┤
│         EF Core DbContext (scoped)           │
│   Code-first migrations. Global query        │
│   filters for soft delete.                   │
├──────────────────────────────────────────────┤
│               PostgreSQL                      │
└──────────────────────────────────────────────┘

Cross-cutting:
 • BuildingScopeFilter (middleware)
 • Room status concurrency guard
 • Tenant auto-filter (service layer)
 • Background jobs (IHostedService)
```

**Pattern:** Controller → Service → EF Core. No repository layer (EF Core IS the repository). Services own all business logic. Controllers are thin — validate input, call service, return result.

---

## §5 Cross-Cutting Patterns

### 5.1 Building-Scoped Authorization

Every endpoint that touches building-owned data passes through a filter:

```
1. OWNER → pass (owns everything)
2. STAFF → check BuildingStaff.Exists(buildingId, staffUserId)
   Not assigned → 403
3. TENANT → entity-level ownership (own contracts, invoices, etc.)
```

The `buildingId` is resolved from: route param, query param, or walked from child entity (Room → Building, Contract → Room → Building, etc.).

### 5.2 Room Status Concurrency Guard

Room.Status is written from 5+ code paths (reservation create/cancel/expire, contract create/terminate, manual PATCH). Race condition risk is real.

**Solution:** One centralized method. All code calls it. Nobody writes Room.Status directly.

```csharp
async Task UpdateRoomStatusAsync(int roomId, RoomStatus newStatus, RoomStatus expectedCurrent)
{
    var room = await _context.Rooms.FindAsync(roomId);
    if (room.Status != expectedCurrent)
        throw new ConflictException($"Room status is {room.Status}, expected {expectedCurrent}");
    room.Status = newStatus;
    await _context.SaveChangesAsync(); // RowVersion check throws DbUpdateConcurrencyException
}
```

### 5.3 Tenant Auto-Filtering

When a TENANT calls `GET /contracts`, `GET /invoices`, `GET /payments`, `GET /issues` — the service layer reads UserId from the JWT and adds `.Where(x => x.TenantUserId == userId)` (or equivalent join). No separate tenant routes needed. Same endpoint, different data based on role.

### 5.4 Soft Delete

Three entities use `DeletedAt`: User, Building, Room. EF Core global query filter: `.HasQueryFilter(e => e.DeletedAt == null)`. All list queries automatically exclude deleted records. Explicit `.IgnoreQueryFilters()` only when needed (admin audit).

---

## §6 Key Algorithms

### 6.1 Invoice Generation

`POST /invoices/generate { buildingId, billingYear, billingMonth }`

For each room with an ACTIVE contract in the building:

```
1. CHECK UNIQUENESS
   If Invoice already exists for (ContractId, BillingYear, BillingMonth) → SKIP, add to skipped[]

2. CALCULATE RENT LINE
   Normal month: Contract.MonthlyRent
   First month (contract started this month):
     prorated = MonthlyRent / daysInMonth × daysFromMoveInToEndOfMonth
   Last month (contract terminating this month):
     prorated = MonthlyRent / daysInMonth × daysFromStartToTerminationDate

3. FOR EACH METERED SERVICE (IsMetered = true):
   a. Check RoomService override → if IsEnabled = false → skip
   b. Get MeterReading for (roomId, serviceId, billingYear, billingMonth)
   c. If no reading → SKIP this service, add to warnings[] (don't block invoice)
   d. price = (CurrentReading - PreviousReading) × (OverrideUnitPrice ?? Service.UnitPrice)
   e. Create InvoiceDetail with PreviousReading + CurrentReading snapshot

4. FOR EACH FLAT SERVICE (IsMetered = false, IsActive = true):
   a. Check RoomService override → if IsEnabled = false → skip
   b. quantity = OverrideQuantity ?? activeOccupantCount ?? 1
   c. price = quantity × (OverrideUnitPrice ?? Service.UnitPrice)
   d. Create InvoiceDetail

5. CREATE INVOICE
   RentAmount = rent line
   ServiceAmount = sum of all service lines
   PenaltyAmount = 0, DiscountAmount = 0
   TotalAmount = Rent + Service + Penalty - Discount
   Status = DRAFT
   DueDate = billingYear-billingMonth+1-Building.InvoiceDueDay (next month)

6. SINGLE DB TRANSACTION for entire batch
```

Returns: `{ generated: Invoice[], skipped: [{ contractId, reason }], warnings: string[] }`

### 6.2 Deposit Lifecycle

Deposits flow through Payment records with type discrimination:

```
RESERVATION CREATED
  → Reservation.DepositAmount recorded (no Payment yet)
  → Room → BOOKED

RESERVATION → CONVERTED (contract created with reservationId)
  → Payment(Type=DEPOSIT_IN, Amount=reservationDeposit, ContractId=new)
  → If contract.DepositAmount > reservation.DepositAmount:
      additional Payment(DEPOSIT_IN) for the difference
  → Contract.DepositStatus = HELD

RESERVATION → CANCELLED
  → Payment(DEPOSIT_IN, Amount=DepositAmount) recorded (money was received)
  → If RefundAmount > 0: Payment(DEPOSIT_REFUND, Amount=RefundAmount)
  → If RefundAmount = 0: deposit forfeited (no REFUND payment)
  → Room → AVAILABLE

CONTRACT TERMINATED
  → RefundAmount = DepositAmount - deductions
  → Payment(DEPOSIT_REFUND, Amount=RefundAmount, ContractId=this)
  → Contract.DepositStatus = REFUNDED | PARTIALLY_REFUNDED | FORFEITED
  → Room → AVAILABLE
```

All Payment.Amount values are **positive**. The Type field determines cash direction: DEPOSIT_IN = money received, DEPOSIT_REFUND = money returned. PnL report uses Type to separate.

### 6.3 Contract Renewal

```
POST /contracts/{id}/renew { newEndDate, newMonthlyRent? }

1. Old contract → TERMINATED (administrative, no deposit refund — deposit carries over)
2. New contract created:
   - Same roomId, same tenantUserId
   - StartDate = old.EndDate + 1
   - EndDate = newEndDate
   - MonthlyRent = newMonthlyRent ?? old.MonthlyRent
   - DepositAmount = old.DepositAmount (carries over)
   - DepositStatus = HELD
3. Room stays OCCUPIED (no status change)
4. ContractTenant records carry over (same roommates)
```

### 6.4 PnL Report Calculation

```
Per month:
  operationalIncome  = SUM(Payment.Amount WHERE Type = RENT_PAYMENT, Invoice.Status != VOID)
  depositsReceived   = SUM(Payment.Amount WHERE Type = DEPOSIT_IN)
  depositsRefunded   = SUM(Payment.Amount WHERE Type = DEPOSIT_REFUND)
  expenses           = SUM(Expense.Amount)
  netOperational     = operationalIncome - expenses
  netCashFlow        = operationalIncome + depositsReceived - depositsRefunded - expenses
```

---

## §7 Response & Error Format

### Success
```json
// Single item (200 or 201)
{ "success": true, "data": { ... }, "message": "Building created successfully" }

// Paginated list (200)
{
  "success": true,
  "data": [ ... ],
  "pagination": { "page": 1, "pageSize": 20, "totalItems": 156, "totalPages": 8 }
}
```

### Errors
```json
// Validation (400)
{ "success": false, "message": "Validation failed",
  "errors": { "Email": ["Email is required"], "Phone": ["Must be 10 digits"] } }

// Business rule conflict (409)
{ "success": false, "message": "Room already has an active contract", "errorCode": "ROOM_OCCUPIED" }

// Not found (404)
{ "success": false, "message": "Building not found" }

// Auth (401/403)
{ "success": false, "message": "Token expired" }
```

### HTTP Status Codes
| Code | When |
|------|------|
| 200 | GET, PUT, PATCH success |
| 201 | POST that creates an entity |
| 204 | DELETE success |
| 400 | Validation / bad input |
| 401 | Not authenticated |
| 403 | Wrong role, deactivated, not assigned to building |
| 404 | Entity not found |
| 409 | Unique violation, business rule conflict |
| 429 | Rate limit exceeded |
| 500 | Server error |

---

## §8 API Conventions

- **Base URL:** `/api/v1`
- **Pagination:** `?page=1&pageSize=20` (defaults). Max pageSize: 100.
- **Sort:** `?sort=createdAt:desc` (default for all list endpoints)
- **Auth header:** `Authorization: Bearer {accessToken}`
- **File uploads:** `multipart/form-data`
- **Date format:** ISO 8601 (`2026-03-01` for DateOnly, `2026-03-01T10:30:00Z` for DateTime)
- **Enum serialization:** String values in JSON (`"ACTIVE"`, not `1`)

---

## §9 Background Jobs

Three timer-based jobs using `IHostedService`. No Hangfire for MVP.

| Job | Schedule | What it does |
|-----|----------|--------------|
| ReservationExpiryJob | Every hour | PENDING/CONFIRMED reservations past `ExpiresAt` → EXPIRED. Room → AVAILABLE. Deposit handled per cancel flow. |
| InvoiceOverdueJob | Daily 00:00 | SENT invoices past `DueDate` → OVERDUE. |
| ContractExpiryAlertJob | Daily 08:00 | ACTIVE contracts where `EndDate ≤ today + 30 days` → create Notification for owner + tenant. |

---

## §10 Tech Stack

| Layer | Choice | Why |
|-------|--------|-----|
| Backend | ASP.NET Core 8 Web API | Team skill. C# ecosystem. |
| Auth | JWT (access) + Refresh Token (DB) | Stateless auth with revocation via DB. BCrypt for passwords. |
| ORM | Entity Framework Core 8 | Code-first migrations. LINQ. Built-in concurrency. |
| Database | PostgreSQL | Free, JSON support, strong on Linux, lower hosting cost. |
| File Storage | Cloudinary | Managed CDN. Avatars, CCCD images, receipts, issue photos. |
| OCR | FPT.AI | Vietnamese CCCD parsing. Returns data only — does not auto-save. |
| PDF | QuestPDF | Free, C#-native. Invoice export with Vietnamese layout. |
| Background | IHostedService | Timer-based. Simple. No Hangfire overhead for MVP. |
| Rate Limiting | ASP.NET Core middleware | Fixed window: login 5/min, forgot-password 3/min. |
| Health Check | ASP.NET Core Health Checks | `GET /health` — DB + Cloudinary connectivity. |
| Unit Tests | xUnit + Moq + FluentAssertions | 70%+ coverage target. |
| API Tests | Postman + Newman | 80%+ endpoint coverage. CI via Newman. |
| CI/CD | GitHub Actions | Build → Test → Coverage on PR. |

---

## §11 Notification Triggers

System generates notifications automatically for these events:

| Trigger | Recipient | Message pattern |
|---------|-----------|-----------------|
| Invoice generated | Tenant | "Hóa đơn tháng {X} đã được tạo" |
| Invoice sent | Tenant | Push notification |
| Invoice overdue | Tenant | "Hóa đơn tháng {X} quá hạn" |
| Issue status changed | Reporter | Status update notification |
| Contract near EndDate (≤30d) | Owner + Tenant | Warning: renewal needed |
| Reservation expiring (1d before) | Staff | Action needed |
| Payment recorded | Tenant | "Đã ghi nhận thanh toán {X} VND" |

---

## §12 Role & Permission Matrix

> Endpoint-level permissions are in `endpoints.csv` (Auth column). This is the summary view.

| Area | OWNER | STAFF (assigned building) | TENANT |
|------|-------|---------------------------|--------|
| Auth (login/refresh/logout/change-pw) | yes | yes | yes |
| Auth (register-staff, forgot/reset-pw) | yes | public | public |
| Users (/me, avatar, dashboard) | yes | yes | yes |
| Users (list/create tenants) | yes | yes | no |
| Users (list staff, deactivate) | yes | no | no |
| Buildings (CRUD) | yes | read (assigned) | no |
| Staff assignment (manage) | yes | no | no |
| Staff assignment (read) | yes | yes (assigned) | no |
| Rooms (create/update/read) | yes | yes (assigned) | read (own) |
| Rooms (delete) | yes | no | no |
| Services (create/update/delete) | yes | no | no |
| Services (read) | yes | yes (assigned) | yes |
| Room Service overrides | yes | yes (assigned) | no |
| Tenant Profiles | yes | yes | own |
| Reservations | yes | yes | no |
| Contracts (create/update/terminate/renew) | yes | yes | no |
| Contracts (read) | yes | yes | own (auto-filtered) |
| Roommates | yes | yes | read (own) |
| Meter Readings (write) | yes | yes | no |
| Meter Readings (read) | yes | yes | own room |
| Invoices (generate/edit/send) | yes | yes | no |
| Invoices (void) | yes | no | no |
| Invoices (read/export) | yes | yes | own (auto-filtered) |
| Payments (record) | yes | yes | no |
| Payments (read) | yes | yes | own |
| Expenses (create/update/read) | yes | yes | no |
| Expenses (delete) | yes | no | no |
| Issues (create/read) | yes | yes | yes (own) |
| Issues (manage status) | yes | yes | no |
| Reports (dashboard-stats) | yes | yes (assigned) | no |
| Reports (PnL) | yes | no | no |
| Notifications | yes | yes | yes (own) |

---

## §13 Design Decisions Log

Decisions that affect multiple files. Rationale preserved so reviewers don't re-ask "why."

| Decision | Rationale |
|----------|-----------|
| No contract auto-expiry | You don't auto-evict tenants. Real-world: contracts often extend informally. Dashboard warns instead. |
| PaidAmount computed, not stored | Stored PaidAmount + separate Payment table = guaranteed sync bug. Compute at read time. |
| Payment.Amount always positive | Negative amounts are confusing in reports. Type field (IN/OUT) handles directionality cleanly. |
| Deposits as Payment records | Deposits are real money movements. Tracking them in Payment makes them visible in PnL automatically. |
| RoomService override entity | Rooms in the same building have different needs (no internet, no parking). Building-level Service alone can't handle this. |
| BillingMonth as int pair (Year + Month) | String "2026-02" requires parsing everywhere. Two ints: clean queries, clean validation. |
| One active contract per room (filtered unique) | Prevents double-booking at DB level. Not just application logic — constraint is structural. |
| Invoice unique per contract+month | Prevents accidental double-generation. Idempotent generate endpoint relies on this. |
| No repository layer | EF Core DbContext IS the repository/unit-of-work. Extra abstraction adds complexity for zero benefit at this scale. |
| Building.InvoiceDueDay | Different landlords prefer different due dates. Hardcoded day-10 would be wrong for many. Range 1-28 avoids month-length issues. |
| DepositStatus is lifecycle, not computed | Unlike PaidAmount (which is an aggregate), DepositStatus changes during coordinated multi-step transactions (terminate → refund → status change). It's a state machine, not a derived value. |
| No walk-in reservations (without User) | Over-engineering for MVP. All reservations require an existing tenant User. |

---

## §14 Sprint Summary

| Sprint | Theme | Endpoint Count | Key Deliverables |
|--------|-------|----------------|------------------|
| 1 | Auth & Users | 16 | JWT login/refresh, password reset, user CRUD, dashboard |
| 2 | Buildings & Rooms | 22 | Building CRUD, room CRUD, services, room service overrides, staff assignment |
| 3 | Profiles & Contracts | 13 | CCCD/OCR, reservations, contract CRUD/terminate/renew |
| 4 | Meters & Invoices | 15 | Roommates, meter readings, invoice generation/send/void, PDF export |
| 5 | Payments & Reports | 18 | Payments, expenses, issues, PnL report, notifications |
| **Total** | | **84** | |

---

## §15 Open Questions

| # | Question | Impact | Blocker for |
|---|----------|--------|-------------|
| 1 | Email delivery: SendGrid or SMTP? | Forgot-password, invoice notifications | Sprint 1 |
| 2 | Mobile app planned? | File upload limits, push notification strategy | Sprint 3+ |
| 3 | Multi-owner support later? | Current: 1 owner. Architecture doesn't block multi-owner. | Post-MVP |

---

*This document is the VISION. CSVs are the WORK PRODUCT derived from it. If they conflict, fix the CSV to match this document. Update this file before writing code. Always.*

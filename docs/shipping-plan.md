# ElysStay Shipping Plan
<!-- Last audited: 2026-03-19. Every claim verified against real code. -->

---

## THE HARD TRUTH

Code compiles. Endpoints exist. Pages render. But **"code exists" is not "product works."**

This document separates what actually delivers value to a customer from what merely fills a controller. The audit below is factual — verified by reading every handler, every page, every Keycloak config. No assumptions.

---

## PART 1: WHAT'S MISLED

These are things that *look* done but will fail the customer in practice. Not bugs — **broken promises disguised as features.**

### M-01: The Primary Money Flow Has No UX Bridge

**The problem:** A reservation (tenant interested → deposit paid) should flow into a contract (tenant signed → moves in). The backend supports this: `CreateContractCommand` accepts `reservationId`, converts the reservation to `Converted`, transfers the deposit into a `Payment(DepositIn)` record. This is correct.

**What's actually broken:** The frontend has NO "Convert to Contract" action. The reservation dialog offers only Confirm and Cancel. After confirming a reservation, the owner must manually navigate to Contracts, create a new contract, and re-enter the same data (room, tenant, deposit). The `reservationId` link — the thing that makes deposits flow correctly — is never passed.

**Why this is critical:** This is the #1 use case of the entire product. Every tenant starts here. If this feels clumsy, the product feels broken.

**Fix:** Add a "Convert to Contract" action to the reservation status dialog when status is `Confirmed`. Pre-fill the contract form with `roomId`, `tenantUserId`, `reservationId`, `depositAmount` from the reservation. One click: confirmed reservation → contract form ready to sign.

---

### M-02: Created Users Cannot Log In

**The problem:** `POST /users/tenants` creates a Keycloak user with a password (auto-generated 12-char if none provided). `POST /users/staff` does the same with a required password. Both work. The Keycloak account is real, email-verified, role-assigned.

**What's actually broken:** The password is never communicated to the new user. The command handler generates it, sends it to Keycloak, and then... discards it. The API response (`UserDto`) does not contain the password. There is no email — Keycloak has **zero SMTP configuration**. Password reset is **disabled** in the realm. The new user literally cannot discover their credentials.

**Why this is critical:** An owner creates 20 tenants. None can log in. The product is useless.

**Fix (two parts):**
1. **Immediate:** Return the generated/provided password in the creation response (one-time display). The frontend shows it to the owner so they can share it. This is how most admin panels work before email is wired.
2. **Soon after:** Configure Keycloak SMTP and enable the "reset password" required action so users can self-serve.

---

### M-03: "Sending" an Invoice Sends Nothing

**The problem:** `PATCH /invoices/{id}/send` changes status from `Draft` to `Sent` and creates a `Notification` record. That's it.

**What's actually broken:** The tenant only sees this if they're actively logged in and check the notification bell. There is no email, no SMS, no push notification. For a product whose core monetization loop is "owner sends invoice → tenant pays," the "sends" part is aspirational.

**Why this is critical:** If tenants don't see invoices, they don't pay. If they don't pay, the product delivers no value.

**Fix (progressive):**
1. **Immediate (this session):** Accept that in-app notification is the MVP channel. But make the notification bell more prominent and ensure the dashboard shows unpaid invoices prominently for tenants.
2. **Next:** Wire Keycloak SMTP or a transactional email service (Resend/Mailgun/etc). Send actual emails on invoice-send and overdue events.

---

### M-04: Notifications Are Invisible Unless You're Looking

**The problem:** Background services (`ContractExpiryAlertBackgroundService`, `InvoiceOverdueBackgroundService`) create `Notification` records. Controllers create them on status changes. The frontend has a `NotificationBell` component.

**What's actually broken:** All notifications go to an in-app bell that requires being logged in. For building owners managing dozens of units, critical alerts (expiring contracts, overdue invoices) disappear into a badge counter. No email digest. No mobile push. No urgency.

**Why this matters:** The background services exist *because* these events are time-sensitive. A notification that nobody sees is not a notification.

**Fix:** Same as M-03 — progressive. In-app is fine for MVP launch if the dashboard and bell are impossible to ignore. Email comes next.

---

### M-05: The UI Speaks The Wrong Language

**The problem:** All business rules, currency (VND), ID validation (CCCD 12 digits), phone format (10 digits Vietnamese) confirm this is built for the Vietnamese rental market.

**What's actually broken:** Every UI label is in English. "Add Building", "Total Rooms", "Record Payment", "Occupancy Rate." A Vietnamese building owner who manages nhà trọ and doesn't read English will bounce immediately.

**Why this is critical:** This is not a technical gap. It's a product-market survival gap. The UI language must match the user's language. Period.

**Fix:** Implement i18n with Vietnamese as the primary locale. This is not optional — it's the difference between a product and a tech demo.

---

### M-06: Keycloak Password Reset Is Disabled

**The problem:** The realm export has `resetPasswordAllowed: false`. There is no SMTP configured. If a user forgets their password, there is no recovery path. They are permanently locked out.

**Why this matters:** Combined with M-02, this means every credential problem becomes an admin problem. Owners will be re-creating users constantly.

**Fix:** Enable `resetPasswordAllowed` in the Keycloak realm. Configure SMTP for the realm so Keycloak can send reset links. This unblocks both M-02's long-term fix and M-03/M-04's email notification path.

---

## PART 2: WHAT ACTUALLY WORKS (Verified)

These flows were audited handler-by-handler, page-by-page. They work end-to-end:

| Flow | Backend | Frontend | Notes |
|------|---------|----------|-------|
| Building CRUD | ~5 endpoints, ownership-scoped | List + form dialog, edit, delete | Default services auto-created on building creation (BD-01) |
| Room CRUD + status | ~9 endpoints, building-scoped | List, detail, form, status toggle | Floor validation against TotalFloors, RowVersion concurrency |
| Service management | ~4 endpoints per building | Part of room detail tabs | Price history tracked (PR-03) |
| Room service overrides | 3 endpoints | Tab in room detail page | Override priority chain correct (PR-04) |
| Staff assignment | 3 endpoints | Staff page + assign dialog | Building-scope authorization works |
| Tenant creation | 1 endpoint + auto-TenantProfile | Create dialog | **Credential delivery broken (M-02)** |
| Tenant profile (manual) | GET + PUT | Profile tab in tenant detail | Works — manual entry of CCCD data |
| Reservation create | 1 endpoint, room→BOOKED | Create dialog | Deposit amount, expiry date, note |
| Reservation confirm/cancel | PATCH with refund logic | Status change dialog | Deposit payment records created correctly |
| Reservation expiry | Background job, hourly | N/A (automated) | PENDING/CONFIRMED past ExpiresAt → EXPIRED |
| Contract creation | Full deposit flow, 2 modes | Contract form dialog | **No conversion UX from reservation (M-01)** |
| Contract renewal | Creates new, terminates old | In contract detail page | StartDate = old.EndDate + 1 |
| Contract termination | Deposit refund calculated | Termination dialog | RefundAmount = DepositAmount - deductions |
| Co-tenant management | Add/remove (soft) | In contract detail | MoveOutDate set on removal |
| Meter reading bulk | Upsert with validation | Editable grid per building | Previous reading auto-fetched |
| Invoice generation | Idempotent, proration, batch | "Generate" button on invoice page | Warnings for missing readings (IG-02) |
| Invoice send | Status change + notification | Batch send available | **No actual delivery to tenant (M-03)** |
| Invoice void | Filtered unique constraint | Action in invoice detail | Voided invoices excluded from P&L |
| Payment recording | Per-invoice, auto-status | From invoice detail page | PaidAmount computed from SUM(Payment) |
| Payment batch | All-or-nothing transaction | Not in UI (API only) | Can be added to FE later |
| Expense CRUD + summary | 5 endpoints + summary | List page with summary cards | Soft-delete enabled |
| Maintenance issues | CRUD + status workflow | List + detail with status | NEW→IN_PROGRESS→RESOLVED→CLOSED |
| Notifications | Create + mark read | Bell popover + page | **Invisible unless logged in (M-04)** |
| Dashboard | Role-based stats | 3 role views with KPI cards | Owner/Staff/Tenant variations |
| P&L report | Monthly aggregation | Cards + monthly table | Voided invoices excluded (PNL-04) |
| Invoice overdue job | Background, hourly | N/A (automated) | SENT past DueDate → OVERDUE + notify |
| Contract expiry alerts | Background, daily | N/A (automated) | 30-day warning → notification |
| Rate limiting | Global 100/min + sensitive 5/min | N/A (server-side) | Custom 429 response |
| Auth (Keycloak) | JWT bearer + auto-provision | AuthProvider + AuthGuard | Role sync on every request |

---

## PART 3: THE FULL SEQUENCE

Everything ships. Nothing is "deferred indefinitely." The order is value-first: what makes the product real for a customer comes before what makes it polished.

### PHASE 1: FIX THE LIES (Make existing features actually work)

> The product claims to do these things. It doesn't. Fix that first.

| # | Task | Layer | Est. Complexity |
|---|------|-------|-----------------|
| 1.1 | **Reservation → Contract conversion UX** — Add "Convert to Contract" action to reservation dialog. Pre-fill contract form from reservation data. Pass `reservationId` so deposit flows correctly. | FE | Medium |
| 1.2 | **Return generated password on user creation** — Include `temporaryPassword` in CreateTenant/CreateStaff API response. Show it ONE TIME in the frontend creation dialog so owner can share it. | BE + FE | Low |
| 1.3 | **Keycloak SMTP + password reset** — Configure realm SMTP (Gmail/Resend/Mailgun). Enable `resetPasswordAllowed`. Users can now recover their own passwords. | Infra | Low-Medium |
| 1.4 | **Manual E2E walkthrough** — Walk the FULL lifecycle: building → rooms → services → tenant → reservation → confirm → contract → meter readings → generate invoice → send → payment → P&L. Document every friction point. | Manual | — |

### PHASE 2: SPEAK THE RIGHT LANGUAGE

> A product that the customer can't read is not a product.

| # | Task | Layer | Est. Complexity |
|---|------|-------|-----------------|
| 2.1 | **i18n infrastructure** — Add `next-intl` or equivalent. Set up Vietnamese as primary locale, English as fallback. Create locale files structure. | FE | Medium |
| 2.2 | **Translate all UI labels** — Every button, heading, column header, form label, error message, empty state, toast, status badge. ~300-500 strings. | FE | High (volume) |
| 2.3 | **Verify VND currency formatting** — All monetary values must display in VND format (₫ suffix, thousand separators). Verify invoice, payment, expense, P&L, dashboard everywhere. | FE | Low |
| 2.4 | **Vietnamese date/time formatting** — dd/MM/yyyy, not MM/dd/yyyy. Verify all date displays and inputs. | FE | Low |

### PHASE 3: MAKE NOTIFICATIONS REAL

> When the system says "notify," something must actually reach the user.

| # | Task | Layer | Est. Complexity |
|---|------|-------|-----------------|
| 3.1 | **Email service integration** — Set up a transactional email provider (Resend is simplest: free tier, API-only, no SMTP relay needed). Create an `IEmailService` abstraction in Infrastructure. | BE | Medium |
| 3.2 | **Invoice sent → email tenant** — When invoice status moves to SENT, email the tenant with invoice summary (amount, due date, building/room). | BE | Low |
| 3.3 | **Invoice overdue → email tenant** — When background job marks invoice OVERDUE, email the tenant. | BE | Low |
| 3.4 | **Contract expiry → email owner** — When background job creates expiry alert, also email the owner. | BE | Low |
| 3.5 | **Payment recorded → email tenant** — When payment is recorded, email confirmation to tenant. | BE | Low |

### PHASE 4: COMPLETE THE FILE INFRASTRUCTURE

> These were called "deferred" before. They're not deferred. They're next in line.

| # | Task | Layer | Est. Complexity |
|---|------|-------|-----------------|
| 4.1 | **Cloudinary integration** — Set up Cloudinary account, add SDK, create `IFileUploadService` abstraction. Config in appsettings. | BE | Medium |
| 4.2 | **Avatar upload** — Wire `POST /users/me/avatar`. Upload to Cloudinary, store URL in User.AvatarUrl. FE: add avatar picker to settings page. | BE + FE | Medium |
| 4.3 | **CCCD image upload** — Wire `POST /tenant-profiles/{userId}/upload-id-images`. Front + back images to Cloudinary. FE: add upload zones to tenant profile tab. | BE + FE | Medium |
| 4.4 | **Expense receipt upload** — Wire `POST /expenses/{id}/receipt`. Upload to Cloudinary, store URL in Expense.ReceiptUrl. FE: add receipt upload to expense form/detail. | BE + FE | Low-Medium |
| 4.5 | **Issue image upload** — Wire `POST /issues/{id}/images`. Up to 3 images per issue. FE: add image upload to issue create/detail. | BE + FE | Medium |
| 4.6 | **CCCD OCR** — Wire `POST /tenant-profiles/{userId}/ocr` with FPT.AI. Extract text → return to FE → user confirms → PUT saves. | BE + FE | Medium |
| 4.7 | **Invoice PDF export** — Wire `GET /invoices/{id}/export` with QuestPDF. Vietnamese-formatted PDF. FE: add "Export PDF" button to invoice detail. | BE + FE | Medium-High |

### PHASE 5: DEPLOYMENT

> A product that only runs on localhost is a prototype.

| # | Task | Layer | Est. Complexity |
|---|------|-------|-----------------|
| 5.1 | **Dockerfile for API** — Multi-stage .NET 10 build. Health check endpoint. Environment variable config. | Infra | Low |
| 5.2 | **Dockerfile for Frontend** — Multi-stage Next.js build. Standalone output mode. | Infra | Low |
| 5.3 | **Production docker-compose** — All 4 services (postgres, keycloak, api, frontend). Proper networking, volumes, health checks. Environment-specific config. | Infra | Medium |
| 5.4 | **Seed data script** — SQL or EF seed: demo building, rooms, a few tenants, sample invoices. So demos aren't empty. | BE | Low-Medium |
| 5.5 | **Deploy to a reachable server** — VPS or cloud instance. Domain name. HTTPS via Caddy/traefik. | Infra | Medium |

### PHASE 6: RESPONSIVE + MOBILE VERIFICATION

> Vietnamese building owners use phones. The app must work on them.

| # | Task | Layer | Est. Complexity |
|---|------|-------|-----------------|
| 6.1 | **Mobile viewport audit** — Test every page on 375px/390px viewport. Document what breaks. | Manual | — |
| 6.2 | **Fix AppShell responsive behavior** — Sidebar collapse, mobile nav, touch targets, scroll behavior. | FE | Medium |
| 6.3 | **Fix forms on mobile** — Dialog forms, date pickers, select dropdowns, table overflow. | FE | Medium |
| 6.4 | **Fix data tables on mobile** — Card view or horizontal scroll for dense tables. | FE | Medium |

### PHASE 7: HARDENING FOR CONFIDENCE

> Not blockers, but things that make us sleep at night.

| # | Task | Layer | Est. Complexity |
|---|------|-------|-----------------|
| 7.1 | **Backend integration tests** — Test the critical flows: reservation→contract→invoice→payment. Use WebApplicationFactory + test DB. | BE | High |
| 7.2 | **Background job observability** — Structured logging of job outcomes (how many processed, errors). Health check integration. | BE | Low-Medium |
| 7.3 | **Frontend error boundaries** — Verify every page has error/loading states. Test with API down. | FE | Low |
| 7.4 | **Production appsettings** — Separate development secrets from production config. No hardcoded credentials in committed files. | BE | Low |

---

## PART 4: THE NON-NEGOTIABLE ORDER

```
Phase 1 → must complete before showing to ANY customer
Phase 2 → must complete before showing to Vietnamese customers
Phase 3 → must complete before asking customers to USE it daily
Phase 4 → completes the full feature vision
Phase 5 → makes the product reachable
Phase 6 → makes the product usable on real devices
Phase 7 → makes us confident it won't break
```

Phase 1 and 2 are the minimum for an honest pilot.
Phase 3 makes the pilot actually useful.
Phase 4+ makes the product complete.

Nothing is deferred. Everything ships. This is the order.

---

## PART 5: KNOWN RISKS

| Risk | Impact | Mitigation |
|------|--------|------------|
| .NET 10 is a preview SDK — Docker base images may not exist or may be unstable | Phase 5 blocked | Check `mcr.microsoft.com/dotnet/aspnet:10.0` availability before starting Phase 5 |
| Keycloak SMTP config change could break existing auth flows | Phase 1.3 dangerous | Export realm config BEFORE any changes. Test in isolation. |
| i18n retrofit across 47 route files is high-volume work | Phase 2 is slow | Prioritize by page importance: dashboard → invoices → contracts → rooms → rest |
| FPT.AI OCR API may require Vietnamese business registration | Phase 4.6 blocked | Research API access requirements early. Have manual fallback. |
| Cloudinary free tier has limits (25 credits/month) | Phase 4 constrained | Sufficient for pilot. Upgrade when customer count justifies it. |
| VPS deployment exposes real security surface | Phase 5 risky | HTTPS mandatory. Keycloak admin console restricted. DB not publicly accessible. |
| No automated tests means regression risk on every change | Ongoing | Phase 7.1 mitigates. Until then: build check + manual smoke after every significant change. |

---

## PART 6: WHAT "DONE" LOOKS LIKE

A customer can:

1. Log into the system (Vietnamese UI)
2. Create a building with rooms and services
3. Add tenants (who receive credentials and can log in)
4. Take a reservation, confirm it, convert it to a contract — in one flow
5. Enter meter readings monthly
6. Generate invoices with correct proration and service charges
7. Send invoices (tenants get notified — in-app, then email)
8. Record payments and see invoice status update automatically
9. Track expenses with receipt images
10. Report maintenance issues with photos
11. See a P&L report that makes financial sense
12. Get alerts when contracts are expiring or invoices are overdue
13. Export an invoice as a PDF to hand to the tenant
14. Access all of this from a phone
15. Access all of this from a URL on the internet, not just localhost

When all 15 are true, the product ships. Not before.

---

*This plan is a living document. Update it as phases complete. Never delete a finding — mark it resolved.*

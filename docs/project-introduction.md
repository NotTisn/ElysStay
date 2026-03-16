**Project Introduction**

In Vietnam, many small rental businesses still run operations through manual ledgers, ad-hoc chat messages, and spreadsheets. The Rental Management System (RMS) exists to digitize that lifecycle: users, buildings, rooms, contracts, meter readings, invoices, payments, maintenance, and reporting.

**Important Context: Vision vs Implementation**

This document describes project direction and product intent.

- **Vision scope (docs):** 84 endpoints, 17 entities, complete lifecycle coverage.
- **Current implementation (codebase):** substantial but partial coverage. Some documented features are still pending.

Always verify runtime truth in controllers/handlers/entities/migrations before assuming a documented feature already exists.

**Current Technical Baseline (as of 2026-03-15)**

- Backend: ASP.NET Core on .NET 10 with MediatR vertical slices.
- Persistence: EF Core + PostgreSQL.
- Auth: Keycloak JWT bearer validation.
- Cross-cutting: global exception middleware, rate limiting, Serilog logging.
- Frontend: Next.js 16 + React 19 with Keycloak-based auth shell.

**Known Implementation Gaps (High-Level)**

- Background jobs for automated expiry/overdue/alert workflows are not yet implemented.
- OCR/file-upload/PDF-related flows are not fully wired end-to-end.
- CI and automated tests require expansion.

**Target Users**

- **Owner:** Full visibility and control of buildings, pricing logic, contracts, invoices, and reporting.
- **Staff:** Building-scoped operational workflows (meter readings, invoice handling, maintenance updates).
- **Tenant:** Visibility into contract/invoice status and issue reporting workflows.
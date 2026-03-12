**Project Introduction**

In Vietnam, small-scale landlords managing *nhà trọ* (rental housing) and *phòng trọ* (rental rooms) heavily rely on manual processes — handwritten ledgers, scattered Zalo messages, and unregulated Google Sheets. This outdated approach inevitably leads to calculation errors, missed utility collections, uncontrolled deposits, and tenant disputes. The Rental Property Management System project aims to solve these pain points by providing a centralized, automated platform that digitizes the entire rental lifecycle, replacing chaotic paperwork with an enforced, error-free operational flow.

**Project Description**

The "Rental Management System" (RMS) serves as the core backend engine designed to empower landlords, their staff, and tenants. Rather than a simple record-keeping tool, RMS acts as a strict financial and operational state machine. It handles multi-building administration, verifies identities via AI, automates monthly billing cycles based on dynamic meter readings, and secures financial transparency from the first deposit to the final check-out.

**Key Features:**

- **Multi-Property & Space Management:** Centralized control for multiple buildings, individual rooms, and highly configurable service fees (e.g., electricity, water, internet) with per-room override capabilities.
- **Smart Onboarding & Contracts:** Frictionless tenant onboarding utilizing FPT.AI for CCCD (National ID) OCR extraction. Enforces a strict lifecycle from room reservation (deposit tracking) to active leasing and seamless contract renewals.
- **Automated Billing Engine:** Eliminates manual math. The system automatically computes monthly invoices combining flat fees and dynamic meter readings (handling previous vs. current meters), complete with automated prorations for mid-month move-ins.
- **Financial & Payment Control:** Granular tracking of all cash flows (partial rent payments, deposit retention/refunds, building expenses). Outputs structured Profit & Loss (PnL) metrics and exports standardized PDF invoices.
- **Maintenance & Ticket System:** A built-in issue tracking pipeline where tenants report room problems (with image uploads) and staff update resolutions via precise status flows (NEW → IN_PROGRESS → RESOLVED).
- **Proactive Automation & Alerts:** Timer-based background jobs that automatically flag overdue invoices, expire pending reservations, and issue alerts for contracts nearing their end dates.

**Project Scope**

This project focuses on the robust design, development, and deployment of the core Web API infrastructure, serving as the foundation for future Web and Mobile applications. The scope strictly adheres to:

- **Platform & Capability:** Developed as a RESTful Web API using ASP.NET Core 8, C#, and Entity Framework Core 8, running on a PostgreSQL database. Totaling exactly 84 endpoints covering 17 complex data entities.
- **Security & Authorization:** Implements stateless JWT authentication, strong BCrypt password hashing, rate-limiting, and deep Building-Scoped Role-Based Access Control (RBAC) ensuring Staff can only modify their assigned territories.
- **Data Integrity:** Employs optimistic concurrency control (RowVersion) to prevent race conditions during room booking, and guarantees idempotency on high-risk financial operations like invoice batch generation.
- **External Integrations:** Leverages Cloudinary for media/document storage, FPT.AI for Vietnamese ID OCR, and QuestPDF for culturally localized invoice rendering.
- **Quality Assurance:** Maintained at >70% test coverage via xUnit for unit logic and Postman/Newman for automated CI/CD endpoint validation.

**Target Users**

- **Landlords (OWNER):** The ultimate decision-makers. They require a bird's-eye view of all properties, full authority to configure pricing logic, access to PnL reports, and the ability to oversee both tenants and staff.
- **Building Staff (STAFF):** The operational workforce. Assigned to specific buildings, they execute day-to-day tasks: logging monthly meter readings, handling tenant issues, sending out generated invoices, and collecting payments.
- **Tenants (TENANT):** The end-consumers. They need transparency and convenience: viewing their lease terms, checking itemized monthly utility bills, making informed payments, and reporting maintenance tickets directly to management.
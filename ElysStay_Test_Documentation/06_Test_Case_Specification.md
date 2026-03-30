# Test Case Specification: ElysStay - Rental Management System

**Version 1.0**
**30/03/2026**
**Document Number:** ELYS-001
**Contract Number:** SE113.O11.PMCL

---

## Table of Contents
1. Introduction
2. Overview
3. Assumptions/Constraints/Risks
   3.1 Assumptions
   3.2 Constraints
   3.3 Risks
4. Test Case Summary
5. Test Case Details
   5.1 Module 1: Auth & Users Management
   5.2 Module 2: Property & Room
   5.3 Module 3: Reservation
   5.4 Module 4: Contract
   5.5 Module 5: Meter Reading
   5.6 Module 6: Invoice Billing
   5.7 Module 7: Payment
   5.8 Module 8: Maintenance Issues
   5.9 Module 9: Expense Tracking
   5.10 Module 10: Service Management
   5.11 Module 11: Dashboard & Reporting
Appendix A: Record of Changes
Appendix B: Acronyms
Appendix C: Glossary
Appendix D: Referenced Documents
Appendix E: Approvals
Appendix F: Additional Appendices

---

## 1. Introduction

**Test Case Specification:** ElysStay Rental Management System
**Identification Number:** ELYS-001
**Title:** ElysStay Test Case Specification
**Version:** 1.0
**Release Number:** 1

The purpose of this document is to outline the test case specification for the ElysStay - Rental Management System. ElysStay is a complex software comprising mobile and web modules requiring intensive data interactions, high-speed queries, and a robust user-friendly interface. It demands knowledge across backend frameworks (.NET 10 API, PostgreSQL) and frontend management. The application will empower users to list their properties and rooms, explore a wide range of available options, automate rental bookings, execute contracts, and conduct secure transactions.

This document has been meticulously crafted through careful planning and rigorous testing processes to guarantee ElysStay's top-notch quality and performance. The target audience for this document encompasses software testers, developers, project managers, and stakeholders engaged in ElysStay's development and implementation.

Anticipate ongoing evolution of this document as the application progresses through additional development and testing phases. Subsequent versions will be issued to update the test case specification, accommodating any alterations or improvements implemented in the application.

## 2. Overview

ElysStay is designed to provide a safe and user-centric environment for real estate and rental property management. The application will empower building owners, staff, and tenants to effectively manage their accommodations, execute digital leases securely, and monitor payments dynamically.

Key features of the application include:
- **User Authentication and RBAC:** Manage Tenants, Owners, and Staff access.
- **Property & Room Management:** View available properties and rooms.
- **Reservation & Contracts:** Automate rental bookings and lifecycle contract management.
- **Invoices & Billing:** Calculate and generate multi-layered monthly invoices.
- **Digital Payments:** Enable digital payments with real-time webhooks.
- **Maintenance Issues:** Empower tenants to report maintenance issues globally.
- **Dashboard Reporting:** Empower administrative users with deep Profit & Loss, Occupancy, and Debt dashboards.

## 3. Assumptions/Constraints/Risks

### 3.1 Assumptions
The formulation and implementation of test cases for ElysStay rely on the following presumptions:
- All features specified in the application's requirements document have been completely developed and are prepared for testing.
- The test cases presuppose a stable testing environment, encompassing dependable hardware, network connectivity, and software platforms (e.g., .NET API up and running, isolated PostgreSQL database).
- The execution of test cases assumes that testers possess access to all essential resources, including the application, pertinent documentation, and any requisite hardware or software.
- The test cases assume the availability of realistic test data (e.g., mock users, properties, payments) that accurately reflects actual user data.

### 3.2 Constraints
- The application is tailored to operate on designated hardware and software platforms. Deviating from these specified environments may alter the execution and outcomes of the test cases.
- The execution of test cases hinges on the accessibility of essential resources, including testing tools (xUnit, Postman, etc.).
- The application necessitates interaction with external payment gateways via webhooks. Changes or issues with these external systems could have an impact on the test cases related to digital payments.

### 3.3 Risks
- Unclear or poorly defined requirements for complex calculations (e.g., multi-layered invoice generation) may result in the generation of ineffective or inaccurate test cases.
- Failure to cover all functionalities (157 detailed test cases) may lead to the oversight of some defects. To counteract this risk, `04_Weekly_Project_Reports.csv` is actively cross-referenced.
- Server/Database downtime during integration testing.

## 4. Test Case Summary

This comprehensive test case summary provides an organized overview of the diverse test cases that will be executed to validate the robustness and functionality of ElysStay. The test cases cover a wide range of system behaviors ensuring a thorough examination of the application's performance.

- **Auth & Users Management:** Verify proper authentication flow and role assignments.
- **Property & Room:** Validate the creation and status changes of buildings and specific rooms.
- **Reservation:** Test booking workflows and double-booking prevention.
- **Contract:** Assess e-signature flows, lease termination, and renewals.
- **Meter Reading:** Test consumption tracking and math operations.
- **Invoice Billing:** Check logic determining accurate combined total from rents and services.
- **Payment:** Validate gateway webhooks and transaction states.
- **Maintenance Issues:** Track ticketing system state changes and resolution tracking.
- **Expense Tracking:** Check deductions and categorizations.
- **Service Management:** Verify variable rate applications per room.
- **Dashboard & Reporting:** Prove real-time analytics calculations (Occupancy, P&L).

## 5. Test Case Details

### 5.1 Module 1: Auth & Users Management
**5.1.1 Test Objective:** Validate login, sign-up, role management, and token expirations.
**5.1.2 Prerequisite Conditions:** System database up, isolated environment.
**5.1.3 Expected Test Results:** Account created/authenticated correctly based on valid payloads. Invalid credentials securely rejected.

### 5.2 Module 2: Property & Room
**5.2.1 Test Objective:** Evaluate building and room creation, editing fields, and status toggles.
**5.2.2 Prerequisite Conditions:** Logged in as Owner/Staff.
**5.2.3 Expected Test Results:** Property lists update correctly. Soft deletions prevent orphaned contracts.

### 5.3 Module 3: Reservation
**5.3.1 Test Objective:** Ensure the sequence of securing a room operates accurately.
**5.3.2 Prerequisite Conditions:** Room must be available. User logged in.
**5.3.3 Expected Test Results:** System locks overlapping dates efficiently. Expiries clear auto-blocks.

### 5.4 Module 4: Contract
**5.4.1 Test Objective:** Validate lifecycle state-machine of physical or digital leases.
**5.4.2 Prerequisite Conditions:** Active Reservation converted to Contract.
**5.4.3 Expected Test Results:** E-signatures logged. Active metrics show tenant assigned.

### 5.5 Module 5: Meter Reading
**5.5.1 Test Objective:** Assess dynamic data input for monthly consumption metrics.
**5.5.2 Prerequisite Conditions:** Active contract. Previous baseline recorded.
**5.5.3 Expected Test Results:** Proper difference calculated. Disallow smaller "current" reading vs "previous".

### 5.6 Module 6: Invoice Billing
**5.6.1 Test Objective:** Verify generation of dynamic consolidated monthly bill.
**5.6.2 Exepcted Test Results:** Mathematical sums of Rent + Services + Utilities match precisely 100%.

### 5.7 Module 7: Payment
**5.7.1 Test Objective:** Track monetary states properly.
**5.7.2 Expected Test Results:** Webhooks mark invoice as PAiD. Partial payments reflect remainder. Overpayments blocked.

### 5.8 Module 8: Maintenance Issues
**5.8.1 Test Objective:** Evaluate ticketing logic and progression.
**5.8.2 Expected Test Results:** Tickets open up, assigned to staff, marked resolved properly upon images upload.

### 5.9 Module 9: Expense Tracking
**5.9.1 Test Objective:** Ensure backend correctly deduces costs globally.
**5.9.2 Expected Test Results:** Uploaded receipts log costs correctly in P&L.

### 5.10 Module 10: Service Management
**5.10.1 Test Objective:** Track supplementary assignments (Parking, WiFi).
**5.10.2 Expected Test Results:** Services properly mapped or removed seamlessly prior to invoicing.

### 5.11 Module 11: Dashboard & Reporting
**5.11.1 Test Objective:** Ensure global overview aggregates heavy logic efficiently.
**5.11.2 Expected Test Results:** High-speed analytical cache returns proper total revenue and occupancy.

---

## Appendix A: Record of Changes
| Version Number | Date | Author/Owner | Description of Change |
| --- | --- | --- | --- |
| 1.0 | 30/03/2026 | Võ Trung Tín | Init the Test Case Specification for ElysStay (157 TCs integrated scope) |

## Appendix B: Acronyms
| Acronym | Literal Translation |
| --- | --- |
| ELYS | ElysStay |
| RBAC | Role-Based Access Control |
| JWT | JSON Web Token |

## Appendix C: Glossary
| Term | Acronym | Definition |
| --- | --- | --- |
| Tenant | Tenant | The person renting the accommodation |
| Owner | Owner | Building administrator |
| Contract | Contract | Agreement for lease terms |
| P&L | PnL | Profit and Loss |

## Appendix D: Referenced Documents
| Document Name | Issuance Date |
| --- | --- |
| 05_Test_Plan_Report.md | 25/03/2026 |
| 04_Weekly_Project_Reports.csv | 30/03/2026 |

## Appendix E: Approvals
| Document Approved By | Date Approved |
| --- | --- |
| Name: Nguyễn Thị Thanh Trúc | 30/03/2026 |

## Appendix F: Additional Appendices
| Document Name | Date |
| --- | --- |
| Testcase Summary (00_Test_Statistics.csv) | 30/03/2026 |
| Unit Test Summary (03_Unit_Test_Report.csv) | 30/03/2026 |
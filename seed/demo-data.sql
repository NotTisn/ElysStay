-- ════════════════════════════════════════════════════════════════════════
-- ElysStay Demo Data Seed
-- ════════════════════════════════════════════════════════════════════════
--
-- Prerequisites:
--   1. EF Core migrations have been applied (tables exist)
--   2. Keycloak realm has been imported with demo users
--
-- Run this script once after initial setup:
--   docker exec -i elys_prod_db psql -U postgres -d ElysStay < seed/demo-data.sql
--
-- Demo accounts (all passwords: Demo@123):
--   demo-owner@elysstay.com   → Chủ nhà (Owner)
--   demo-staff@elysstay.com   → Nhân viên (Staff)
--   demo-tenant1@elysstay.com → Khách thuê 1 (Tenant)
--   demo-tenant2@elysstay.com → Khách thuê 2 (Tenant)
--   demo-tenant3@elysstay.com → Khách thuê 3 (Tenant)
--
-- ════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── Fixed UUIDs ──────────────────────────────────────────────────────
-- Keycloak IDs (must match realm export)
-- Owner:   a0000000-0000-0000-0000-000000000001
-- Staff:   a0000000-0000-0000-0000-000000000002
-- Tenant1: a0000000-0000-0000-0000-000000000003
-- Tenant2: a0000000-0000-0000-0000-000000000004
-- Tenant3: a0000000-0000-0000-0000-000000000005

-- DB entity IDs
-- Building:     b0000001-0000-0000-0000-000000000001
-- Rooms:        c0000001-...-000000000001 through 006
-- Services:     e0000001-...-000000000001 through 003
-- Contracts:    f0000001-...-000000000001, 002
-- Invoices:     10000001-...-000000000001
-- Payments:     11000001-...-000000000001, 002

-- ── 1. Users ─────────────────────────────────────────────────────────

INSERT INTO "Users" ("Id", "KeycloakId", "Email", "FullName", "Phone", "AvatarUrl", "Role", "Status", "CreatedAt", "UpdatedAt", "DeletedAt")
VALUES
  ('b0000001-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 'demo-owner@elysstay.com', 'Nguyễn Văn An', '0901234567', NULL, 'Owner', 'Active', NOW(), NOW(), NULL),
  ('b0000001-0000-0000-0000-000000000002', 'a0000000-0000-0000-0000-000000000002', 'demo-staff@elysstay.com', 'Trần Thị Bình', '0912345678', NULL, 'Staff', 'Active', NOW(), NOW(), NULL),
  ('b0000001-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000003', 'demo-tenant1@elysstay.com', 'Lê Hoàng Cường', '0923456789', NULL, 'Tenant', 'Active', NOW(), NOW(), NULL),
  ('b0000001-0000-0000-0000-000000000004', 'a0000000-0000-0000-0000-000000000004', 'demo-tenant2@elysstay.com', 'Phạm Minh Dũng', '0934567890', NULL, 'Tenant', 'Active', NOW(), NOW(), NULL),
  ('b0000001-0000-0000-0000-000000000005', 'a0000000-0000-0000-0000-000000000005', 'demo-tenant3@elysstay.com', 'Võ Thanh Hùng', '0945678901', NULL, 'Tenant', 'Active', NOW(), NOW(), NULL)
ON CONFLICT ("Id") DO NOTHING;

-- ── 2. Tenant Profiles ───────────────────────────────────────────────

INSERT INTO "TenantProfiles" ("Id", "UserId", "IdNumber", "DateOfBirth", "Gender", "PermanentAddress", "IssuedDate", "IssuedPlace", "IdFrontUrl", "IdBackUrl", "CreatedAt", "UpdatedAt")
VALUES
  ('d0000001-0000-0000-0000-000000000001', 'b0000001-0000-0000-0000-000000000003', '012345678901', '1995-03-15', 'Nam', '123 Nguyễn Trãi, Q.1, TP.HCM', '2020-06-01', 'CA TP.HCM', NULL, NULL, NOW(), NOW()),
  ('d0000001-0000-0000-0000-000000000002', 'b0000001-0000-0000-0000-000000000004', '012345678902', '1998-07-22', 'Nam', '456 Lê Lợi, Q.3, TP.HCM', '2021-01-15', 'CA TP.HCM', NULL, NULL, NOW(), NOW()),
  ('d0000001-0000-0000-0000-000000000003', 'b0000001-0000-0000-0000-000000000005', '012345678903', '2000-11-10', 'Nam', '789 Trần Hưng Đạo, Q.5, TP.HCM', '2022-03-20', 'CA TP.HCM', NULL, NULL, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

-- ── 3. Building ──────────────────────────────────────────────────────

INSERT INTO "Buildings" ("Id", "OwnerId", "Name", "Address", "Description", "TotalFloors", "InvoiceDueDay", "CreatedAt", "UpdatedAt", "DeletedAt")
VALUES
  ('b0000001-0000-0000-0000-000000000010', 'b0000001-0000-0000-0000-000000000001', 'Nhà Trọ An Phú', '123 Nguyễn Văn Cừ, Q.5, TP.HCM', 'Nhà trọ 3 tầng, 6 phòng, gần trường đại học. Có chỗ để xe, giặt đồ miễn phí.', 3, 5, NOW(), NOW(), NULL)
ON CONFLICT ("Id") DO NOTHING;

-- ── 4. Staff Assignment ──────────────────────────────────────────────

INSERT INTO "StaffAssignments" ("BuildingId", "StaffId", "AssignedAt")
VALUES
  ('b0000001-0000-0000-0000-000000000010', 'b0000001-0000-0000-0000-000000000002', NOW())
ON CONFLICT ("BuildingId", "StaffId") DO NOTHING;

-- ── 5. Services (building-level defaults) ────────────────────────────

INSERT INTO "Services" ("Id", "BuildingId", "Name", "Unit", "UnitPrice", "PreviousUnitPrice", "PriceUpdatedAt", "IsMetered", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
  ('e0000001-0000-0000-0000-000000000001', 'b0000001-0000-0000-0000-000000000010', 'Điện', 'kWh', 3500, NULL, NULL, TRUE, TRUE, NOW(), NOW()),
  ('e0000001-0000-0000-0000-000000000002', 'b0000001-0000-0000-0000-000000000010', 'Nước', 'm³', 15000, NULL, NULL, TRUE, TRUE, NOW(), NOW()),
  ('e0000001-0000-0000-0000-000000000003', 'b0000001-0000-0000-0000-000000000010', 'Internet', 'tháng', 100000, NULL, NULL, FALSE, TRUE, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

-- ── 6. Rooms (6 rooms across 3 floors) ──────────────────────────────

INSERT INTO "Rooms" ("Id", "BuildingId", "RoomNumber", "Floor", "Area", "Price", "MaxOccupants", "Description", "Status", "Images", "RowVersion", "CreatedAt", "UpdatedAt", "DeletedAt")
VALUES
  ('c0000001-0000-0000-0000-000000000001', 'b0000001-0000-0000-0000-000000000010', '101', 1, 20.00, 3500000, 2, 'Phòng tầng trệt, có cửa sổ', 'Occupied', NULL, '\x00000001', NOW(), NOW(), NULL),
  ('c0000001-0000-0000-0000-000000000002', 'b0000001-0000-0000-0000-000000000010', '102', 1, 18.00, 3200000, 2, 'Phòng tầng trệt, gần nhà bếp', 'Occupied', NULL, '\x00000001', NOW(), NOW(), NULL),
  ('c0000001-0000-0000-0000-000000000003', 'b0000001-0000-0000-0000-000000000010', '201', 2, 22.00, 3800000, 3, 'Phòng rộng tầng 2, ban công', 'Available', NULL, '\x00000001', NOW(), NOW(), NULL),
  ('c0000001-0000-0000-0000-000000000004', 'b0000001-0000-0000-0000-000000000010', '202', 2, 20.00, 3500000, 2, 'Phòng tầng 2, view đẹp', 'Booked', NULL, '\x00000001', NOW(), NOW(), NULL),
  ('c0000001-0000-0000-0000-000000000005', 'b0000001-0000-0000-0000-000000000010', '301', 3, 25.00, 4200000, 3, 'Phòng lớn tầng 3, gác lửng', 'Available', NULL, '\x00000001', NOW(), NOW(), NULL),
  ('c0000001-0000-0000-0000-000000000006', 'b0000001-0000-0000-0000-000000000010', '302', 3, 20.00, 3500000, 2, 'Phòng tầng 3, yên tĩnh', 'Maintenance', NULL, '\x00000001', NOW(), NOW(), NULL)
ON CONFLICT ("Id") DO NOTHING;

-- ── 7. Room Services (all rooms get all services) ────────────────────

INSERT INTO "RoomServices" ("Id", "RoomId", "ServiceId", "IsEnabled", "OverrideUnitPrice", "OverrideQuantity")
VALUES
  -- Room 101
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000001', 'e0000001-0000-0000-0000-000000000001', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000001', 'e0000001-0000-0000-0000-000000000002', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000001', 'e0000001-0000-0000-0000-000000000003', TRUE, NULL, NULL),
  -- Room 102
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000002', 'e0000001-0000-0000-0000-000000000001', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000002', 'e0000001-0000-0000-0000-000000000002', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000002', 'e0000001-0000-0000-0000-000000000003', TRUE, NULL, NULL),
  -- Room 201
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000003', 'e0000001-0000-0000-0000-000000000001', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000003', 'e0000001-0000-0000-0000-000000000002', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000003', 'e0000001-0000-0000-0000-000000000003', TRUE, NULL, NULL),
  -- Room 202
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000004', 'e0000001-0000-0000-0000-000000000001', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000004', 'e0000001-0000-0000-0000-000000000002', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000004', 'e0000001-0000-0000-0000-000000000003', TRUE, NULL, NULL),
  -- Room 301
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000005', 'e0000001-0000-0000-0000-000000000001', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000005', 'e0000001-0000-0000-0000-000000000002', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000005', 'e0000001-0000-0000-0000-000000000003', TRUE, NULL, NULL),
  -- Room 302
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000006', 'e0000001-0000-0000-0000-000000000001', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000006', 'e0000001-0000-0000-0000-000000000002', TRUE, NULL, NULL),
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000006', 'e0000001-0000-0000-0000-000000000003', TRUE, NULL, NULL)
ON CONFLICT DO NOTHING;

-- ── 8. Reservation (pending for room 202) ────────────────────────────

INSERT INTO "RoomReservations" ("Id", "RoomId", "TenantUserId", "DepositAmount", "Status", "ExpiresAt", "Note", "RefundAmount", "RefundedAt", "RefundNote", "CreatedAt", "UpdatedAt")
VALUES
  ('a1000001-0000-0000-0000-000000000001', 'c0000001-0000-0000-0000-000000000004', 'b0000001-0000-0000-0000-000000000005', 3500000, 'Pending', NOW() + INTERVAL '7 days', 'Khách quan tâm phòng 202, hẹn xem phòng cuối tuần', NULL, NULL, NULL, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

-- ── 9. Contracts (2 active: rooms 101 and 102) ──────────────────────

INSERT INTO "Contracts" ("Id", "RoomId", "TenantUserId", "ReservationId", "StartDate", "EndDate", "MoveInDate", "MonthlyRent", "DepositAmount", "DepositStatus", "Status", "TerminationDate", "TerminationNote", "RefundAmount", "Note", "CreatedBy", "CreatedAt", "UpdatedAt")
VALUES
  -- Contract for room 101, tenant Cường
  ('f0000001-0000-0000-0000-000000000001', 'c0000001-0000-0000-0000-000000000001', 'b0000001-0000-0000-0000-000000000003', NULL,
   (CURRENT_DATE - INTERVAL '3 months')::date, (CURRENT_DATE + INTERVAL '9 months')::date, (CURRENT_DATE - INTERVAL '3 months')::date,
   3500000, 7000000, 'Held', 'Active', NULL, NULL, NULL,
   'Hợp đồng 12 tháng, đã đặt cọc 2 tháng tiền phòng',
   'b0000001-0000-0000-0000-000000000001', NOW() - INTERVAL '3 months', NOW()),

  -- Contract for room 102, tenant Dũng
  ('f0000001-0000-0000-0000-000000000002', 'c0000001-0000-0000-0000-000000000002', 'b0000001-0000-0000-0000-000000000004', NULL,
   (CURRENT_DATE - INTERVAL '1 month')::date, (CURRENT_DATE + INTERVAL '11 months')::date, (CURRENT_DATE - INTERVAL '1 month')::date,
   3200000, 6400000, 'Held', 'Active', NULL, NULL, NULL,
   'Hợp đồng 12 tháng, sinh viên',
   'b0000001-0000-0000-0000-000000000001', NOW() - INTERVAL '1 month', NOW())
ON CONFLICT ("Id") DO NOTHING;

-- ── 10. Contract Tenants (main tenants) ──────────────────────────────

INSERT INTO "ContractTenants" ("Id", "ContractId", "TenantUserId", "IsMainTenant", "MoveInDate", "MoveOutDate")
VALUES
  (gen_random_uuid(), 'f0000001-0000-0000-0000-000000000001', 'b0000001-0000-0000-0000-000000000003', TRUE, (CURRENT_DATE - INTERVAL '3 months')::date, NULL),
  (gen_random_uuid(), 'f0000001-0000-0000-0000-000000000002', 'b0000001-0000-0000-0000-000000000004', TRUE, (CURRENT_DATE - INTERVAL '1 month')::date, NULL)
ON CONFLICT DO NOTHING;

-- ── 11. Deposit Payments ─────────────────────────────────────────────

INSERT INTO "Payments" ("Id", "InvoiceId", "ContractId", "ReservationId", "Type", "Amount", "PaymentMethod", "PaidAt", "ReferenceCode", "Note", "RecordedBy", "CreatedAt")
VALUES
  -- Deposit for contract 1
  ('11000001-0000-0000-0000-000000000001', NULL, 'f0000001-0000-0000-0000-000000000001', NULL, 'DepositIn', 7000000, 'Chuyển khoản', NOW() - INTERVAL '3 months', 'DEP-101-001', 'Cọc 2 tháng phòng 101', 'b0000001-0000-0000-0000-000000000001', NOW() - INTERVAL '3 months'),
  -- Deposit for contract 2
  ('11000001-0000-0000-0000-000000000002', NULL, 'f0000001-0000-0000-0000-000000000002', NULL, 'DepositIn', 6400000, 'Tiền mặt', NOW() - INTERVAL '1 month', 'DEP-102-001', 'Cọc 2 tháng phòng 102', 'b0000001-0000-0000-0000-000000000001', NOW() - INTERVAL '1 month')
ON CONFLICT ("Id") DO NOTHING;

-- ── 12. Meter Readings (last month for occupied rooms) ───────────────

INSERT INTO "MeterReadings" ("Id", "RoomId", "ServiceId", "BillingYear", "BillingMonth", "PreviousReading", "CurrentReading", "Consumption", "DateRead", "CreatedBy", "CreatedAt", "UpdatedAt")
VALUES
  -- Room 101 electricity
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000001', 'e0000001-0000-0000-0000-000000000001',
   EXTRACT(YEAR FROM CURRENT_DATE - INTERVAL '1 month')::int,
   EXTRACT(MONTH FROM CURRENT_DATE - INTERVAL '1 month')::int,
   100, 245, 145, NOW() - INTERVAL '5 days', 'b0000001-0000-0000-0000-000000000002', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days'),
  -- Room 101 water
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000001', 'e0000001-0000-0000-0000-000000000002',
   EXTRACT(YEAR FROM CURRENT_DATE - INTERVAL '1 month')::int,
   EXTRACT(MONTH FROM CURRENT_DATE - INTERVAL '1 month')::int,
   10, 18, 8, NOW() - INTERVAL '5 days', 'b0000001-0000-0000-0000-000000000002', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days'),
  -- Room 102 electricity
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000002', 'e0000001-0000-0000-0000-000000000001',
   EXTRACT(YEAR FROM CURRENT_DATE - INTERVAL '1 month')::int,
   EXTRACT(MONTH FROM CURRENT_DATE - INTERVAL '1 month')::int,
   50, 180, 130, NOW() - INTERVAL '5 days', 'b0000001-0000-0000-0000-000000000002', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days'),
  -- Room 102 water
  (gen_random_uuid(), 'c0000001-0000-0000-0000-000000000002', 'e0000001-0000-0000-0000-000000000002',
   EXTRACT(YEAR FROM CURRENT_DATE - INTERVAL '1 month')::int,
   EXTRACT(MONTH FROM CURRENT_DATE - INTERVAL '1 month')::int,
   5, 12, 7, NOW() - INTERVAL '5 days', 'b0000001-0000-0000-0000-000000000002', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days')
ON CONFLICT DO NOTHING;

-- ── 13. Invoice (last month, room 101, sent) ─────────────────────────

INSERT INTO "Invoices" ("Id", "ContractId", "BillingYear", "BillingMonth", "RentAmount", "ServiceAmount", "PenaltyAmount", "DiscountAmount", "TotalAmount", "Status", "DueDate", "Note", "CreatedBy", "CreatedAt", "UpdatedAt")
VALUES
  ('10000001-0000-0000-0000-000000000001', 'f0000001-0000-0000-0000-000000000001',
   EXTRACT(YEAR FROM CURRENT_DATE - INTERVAL '1 month')::int,
   EXTRACT(MONTH FROM CURRENT_DATE - INTERVAL '1 month')::int,
   3500000, 727500, 0, 0, 4227500, 'Sent',
   (DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '4 days')::date,
   NULL, 'b0000001-0000-0000-0000-000000000001',
   NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days')
ON CONFLICT ("Id") DO NOTHING;

-- Invoice details (rent + electricity + water + internet)
INSERT INTO "InvoiceDetails" ("Id", "InvoiceId", "ServiceId", "Description", "UnitPrice", "Quantity", "Amount", "PreviousReading", "CurrentReading")
VALUES
  (gen_random_uuid(), '10000001-0000-0000-0000-000000000001', NULL, 'Tiền phòng', 3500000, 1, 3500000, NULL, NULL),
  (gen_random_uuid(), '10000001-0000-0000-0000-000000000001', 'e0000001-0000-0000-0000-000000000001', 'Điện', 3500, 145, 507500, 100, 245),
  (gen_random_uuid(), '10000001-0000-0000-0000-000000000001', 'e0000001-0000-0000-0000-000000000002', 'Nước', 15000, 8, 120000, 10, 18),
  (gen_random_uuid(), '10000001-0000-0000-0000-000000000001', 'e0000001-0000-0000-0000-000000000003', 'Internet', 100000, 1, 100000, NULL, NULL)
ON CONFLICT DO NOTHING;

-- ── 14. Expense ──────────────────────────────────────────────────────

INSERT INTO "Expenses" ("Id", "BuildingId", "RoomId", "Category", "Description", "Amount", "ReceiptUrl", "ExpenseDate", "RecordedBy", "CreatedAt", "UpdatedAt", "DeletedAt")
VALUES
  ('12000001-0000-0000-0000-000000000001', 'b0000001-0000-0000-0000-000000000010', 'c0000001-0000-0000-0000-000000000006', 'Sửa chữa', 'Sửa ống nước phòng 302', 850000, NULL,
   (CURRENT_DATE - INTERVAL '2 days')::date,
   'b0000001-0000-0000-0000-000000000001', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', NULL),
  ('12000001-0000-0000-0000-000000000002', 'b0000001-0000-0000-0000-000000000010', NULL, 'Vệ sinh', 'Dọn vệ sinh hành lang tháng này', 500000, NULL,
   (CURRENT_DATE - INTERVAL '1 day')::date,
   'b0000001-0000-0000-0000-000000000002', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', NULL)
ON CONFLICT ("Id") DO NOTHING;

-- ── 15. Maintenance Issue ────────────────────────────────────────────

INSERT INTO "MaintenanceIssues" ("Id", "BuildingId", "RoomId", "ReportedBy", "AssignedTo", "Title", "Description", "ImageUrls", "Status", "Priority", "CreatedAt", "UpdatedAt")
VALUES
  ('13000001-0000-0000-0000-000000000001', 'b0000001-0000-0000-0000-000000000010', 'c0000001-0000-0000-0000-000000000006', 'b0000001-0000-0000-0000-000000000002', 'b0000001-0000-0000-0000-000000000002',
   'Ống nước rò rỉ phòng 302', 'Ống nước dưới bồn rửa bị rò rỉ, nước chảy xuống sàn. Cần thay ống mới.', NULL,
   'InProgress', 'High', NOW() - INTERVAL '3 days', NOW() - INTERVAL '1 day'),
  ('13000001-0000-0000-0000-000000000002', 'b0000001-0000-0000-0000-000000000010', 'c0000001-0000-0000-0000-000000000001', 'b0000001-0000-0000-0000-000000000003', NULL,
   'Bóng đèn hành lang tầng 1 hỏng', 'Bóng đèn LED hành lang tầng 1 không sáng từ hôm qua.', NULL,
   'New', 'Low', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day')
ON CONFLICT ("Id") DO NOTHING;

-- ── 16. Notifications ────────────────────────────────────────────────

INSERT INTO "Notifications" ("Id", "UserId", "Title", "Message", "IsRead", "Type", "ReferenceId", "CreatedAt")
VALUES
  -- Invoice sent notification for tenant Cường
  (gen_random_uuid(), 'b0000001-0000-0000-0000-000000000003', 'Hóa đơn mới', 'Bạn có hóa đơn mới cho phòng 101. Vui lòng thanh toán trước hạn.', FALSE, 'InvoiceSent', '10000001-0000-0000-0000-000000000001', NOW() - INTERVAL '3 days'),
  -- Maintenance assigned notification for staff
  (gen_random_uuid(), 'b0000001-0000-0000-0000-000000000002', 'Báo cáo sự cố mới', 'Sự cố mới được báo cáo: Ống nước rò rỉ phòng 302. Mức ưu tiên: Cao.', TRUE, 'IssueAssigned', '13000001-0000-0000-0000-000000000001', NOW() - INTERVAL '3 days'),
  -- New issue notification for owner
  (gen_random_uuid(), 'b0000001-0000-0000-0000-000000000001', 'Sự cố mới', 'Có 2 sự cố mới cần xử lý tại Nhà Trọ An Phú.', FALSE, 'IssueCreated', NULL, NOW() - INTERVAL '1 day')
ON CONFLICT DO NOTHING;

COMMIT;

-- ════════════════════════════════════════════════════════════════════════
-- Demo scenario summary:
--
-- Building: Nhà Trọ An Phú (3 tầng, 6 phòng)
--   Room 101 (Occupied) → Tenant Cường, active contract, invoice sent
--   Room 102 (Occupied) → Tenant Dũng, active contract, no invoice yet
--   Room 201 (Available) → Empty, ready for rental
--   Room 202 (Booked)    → Tenant Hùng has pending reservation
--   Room 301 (Available) → Empty, ready for rental
--   Room 302 (Maintenance) → Plumbing issue being fixed
--
-- Services: Điện (3,500₫/kWh), Nước (15,000₫/m³), Internet (100,000₫/tháng)
-- 
-- Active flows to demo:
--   • Confirm reservation on room 202 → convert to contract
--   • Record payment for invoice on room 101
--   • Generate invoice for room 102
--   • Resolve maintenance issue on room 302
--   • Check dashboard KPIs and P&L report
-- ════════════════════════════════════════════════════════════════════════

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDatabaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Rooms_RoomId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_RepresentativeId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractTenants_Users_TenantId",
                table: "ContractTenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Rooms_RoomId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_RecordedById",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Buildings_BuildingId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_TenantId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceIssues_Users_HandledById",
                table: "MaintenanceIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceIssues_Users_TenantId",
                table: "MaintenanceIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_MeterReadings_Contracts_ContractId",
                table: "MeterReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_MeterReadings_Users_RecordedById",
                table: "MeterReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_ReceiverId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_SenderId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_PayerId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_ReceiverId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomReservations_Buildings_BuildingId",
                table: "RoomReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomReservations_Rooms_RoomId",
                table: "RoomReservations");

            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenantProfiles",
                table: "TenantProfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaffAssignments",
                table: "StaffAssignments");

            migrationBuilder.DropIndex(
                name: "IX_StaffAssignments_BuildingId",
                table: "StaffAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_BuildingId_RoomCode",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SenderId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_ContractId_BillingMonth_BillingYear",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_RecordedById",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_RoomId",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_BuildingId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceCode",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BuildingId_ExpenseDate",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_RoomId",
                table: "Expenses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ContractTenants",
                table: "ContractTenants");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ContractCode",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Hometown",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "BaseRent",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "RoomCode",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "ReservationFee",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "SenderId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "NewElectricityIndex",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "NewWaterIndex",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "OldElectricityIndex",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "OldWaterIndex",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "RecordedById",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "HandledAt",
                table: "MaintenanceIssues");

            migrationBuilder.DropColumn(
                name: "Images",
                table: "MaintenanceIssues");

            migrationBuilder.DropColumn(
                name: "IssueType",
                table: "MaintenanceIssues");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "MaintenanceIssues");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ElectricityCharge",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ElectricityUsage",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RemainingAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RoomCharge",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ServiceFees",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "WaterCharge",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "WaterUsage",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ContractCode",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ElectricityRate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "InitialElectricityIndex",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "InitialWaterIndex",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PaymentCycle",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "RefundedDepositAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "RentAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ServiceFees",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WaterRate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AddressCity",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "AddressDistrict",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "AddressNumber",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "AddressStreet",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "AddressWard",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "Images",
                table: "Buildings");

            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                table: "Users",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "Occupation",
                table: "TenantProfiles",
                newName: "PermanentAddress");

            migrationBuilder.RenameColumn(
                name: "IdCardFrontUrl",
                table: "TenantProfiles",
                newName: "IssuedPlace");

            migrationBuilder.RenameColumn(
                name: "IdCardBackUrl",
                table: "TenantProfiles",
                newName: "IdCardFront");

            migrationBuilder.RenameColumn(
                name: "FloorNumber",
                table: "Rooms",
                newName: "Floor");

            migrationBuilder.RenameColumn(
                name: "ExpectedMoveInDate",
                table: "RoomReservations",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "BuildingId",
                table: "RoomReservations",
                newName: "TenantUserId");

            migrationBuilder.RenameIndex(
                name: "IX_RoomReservations_BuildingId",
                table: "RoomReservations",
                newName: "IX_RoomReservations_TenantUserId");

            migrationBuilder.RenameColumn(
                name: "TransferDetails",
                table: "Payments",
                newName: "ReferenceCode");

            migrationBuilder.RenameColumn(
                name: "ReceiverId",
                table: "Payments",
                newName: "RecordedBy");

            migrationBuilder.RenameColumn(
                name: "ReceiptImageUrl",
                table: "Payments",
                newName: "Note");

            migrationBuilder.RenameColumn(
                name: "PayerId",
                table: "Payments",
                newName: "ContractId");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_ReceiverId",
                table: "Payments",
                newName: "IX_Payments_RecordedBy");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_PayerId",
                table: "Payments",
                newName: "IX_Payments_ContractId");

            migrationBuilder.RenameColumn(
                name: "ReceiverId",
                table: "Notifications",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Notifications",
                newName: "Message");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_ReceiverId",
                table: "Notifications",
                newName: "IX_Notifications_UserId");

            migrationBuilder.RenameColumn(
                name: "ReadingDate",
                table: "MeterReadings",
                newName: "DateRead");

            migrationBuilder.RenameColumn(
                name: "ContractId",
                table: "MeterReadings",
                newName: "ServiceId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "MaintenanceIssues",
                newName: "ReportedBy");

            migrationBuilder.RenameColumn(
                name: "ResolutionNotes",
                table: "MaintenanceIssues",
                newName: "ImageUrl");

            migrationBuilder.RenameColumn(
                name: "ReportedAt",
                table: "MaintenanceIssues",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "HandledById",
                table: "MaintenanceIssues",
                newName: "AssignedTo");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceIssues_TenantId",
                table: "MaintenanceIssues",
                newName: "IX_MaintenanceIssues_ReportedBy");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceIssues_HandledById",
                table: "MaintenanceIssues",
                newName: "IX_MaintenanceIssues_AssignedTo");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Invoices",
                newName: "CreatedBy");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_TenantId",
                table: "Invoices",
                newName: "IX_Invoices_CreatedBy");

            migrationBuilder.RenameColumn(
                name: "RecordedById",
                table: "Expenses",
                newName: "RecordedBy");

            migrationBuilder.RenameColumn(
                name: "ReceiptImageUrl",
                table: "Expenses",
                newName: "ReceiptUrl");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_RecordedById",
                table: "Expenses",
                newName: "IX_Expenses_RecordedBy");

            migrationBuilder.RenameColumn(
                name: "RepresentativeId",
                table: "Contracts",
                newName: "TenantUserId");

            migrationBuilder.RenameColumn(
                name: "DepositRefundDate",
                table: "Contracts",
                newName: "TerminationDate");

            migrationBuilder.RenameColumn(
                name: "ContractFileUrl",
                table: "Contracts",
                newName: "TerminationReason");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_RepresentativeId",
                table: "Contracts",
                newName: "IX_Contracts_TenantUserId");

            migrationBuilder.RenameColumn(
                name: "SharedAmenities",
                table: "Buildings",
                newName: "Address");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiry",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdentityCard",
                table: "TenantProfiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "TenantProfiles",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "TenantProfiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TenantProfiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "IdCardBack",
                table: "TenantProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedDate",
                table: "TenantProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TenantProfiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Images",
                table: "Rooms",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Area",
                table: "Rooms",
                type: "numeric(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Rooms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Rooms",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RoomNumber",
                table: "Rooms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Rooms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "RoomReservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "RoomReservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "RoomReservations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "RoomReservations",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundDate",
                table: "RoomReservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "Payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<decimal>(
                name: "Consumption",
                table: "MeterReadings",
                type: "numeric(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "MeterReadings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentReading",
                table: "MeterReadings",
                type: "numeric(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousReading",
                table: "MeterReadings",
                type: "numeric(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "MaintenanceIssues",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MaintenanceIssues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PenaltyAmount",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomAmount",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceAmount",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Expenses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Expenses",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Expenses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "ContractTenants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "MoveInDate",
                table: "ContractTenants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "MoveOutDate",
                table: "ContractTenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DepositAmount",
                table: "Contracts",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Contracts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "Contracts",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "Contracts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomPrice",
                table: "Contracts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Buildings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Buildings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenantProfiles",
                table: "TenantProfiles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaffAssignments",
                table: "StaffAssignments",
                columns: new[] { "BuildingId", "StaffId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ContractTenants",
                table: "ContractTenants",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PreviousUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PriceUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsMetered = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PreviousReading = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    CurrentReading = table.Column<decimal>(type: "numeric(18,3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RoomServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OverrideUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    OverrideQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomServices_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantProfiles_UserId",
                table: "TenantProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAssignments_StaffId",
                table: "StaffAssignments",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BuildingId_RoomNumber",
                table: "Rooms",
                columns: new[] { "BuildingId", "RoomNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_CreatedBy",
                table: "MeterReadings",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_RoomId_ServiceId_BillingYear_BillingMonth",
                table: "MeterReadings",
                columns: new[] { "RoomId", "ServiceId", "BillingYear", "BillingMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_ServiceId",
                table: "MeterReadings",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_BillingYear_BillingMonth",
                table: "Invoices",
                columns: new[] { "ContractId", "BillingYear", "BillingMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BuildingId",
                table: "Expenses",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTenants_ContractId_TenantId",
                table: "ContractTenants",
                columns: new[] { "ContractId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreatedBy",
                table: "Contracts",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ReservationId",
                table: "Contracts",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_OwnerId",
                table: "Buildings",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_InvoiceId",
                table: "InvoiceDetails",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_ServiceId",
                table: "InvoiceDetails",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomServices_RoomId_ServiceId",
                table: "RoomServices",
                columns: new[] { "RoomId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomServices_ServiceId",
                table: "RoomServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_BuildingId",
                table: "Services",
                column: "BuildingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Users_OwnerId",
                table: "Buildings",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_RoomReservations_ReservationId",
                table: "Contracts",
                column: "ReservationId",
                principalTable: "RoomReservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Rooms_RoomId",
                table: "Contracts",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_CreatedBy",
                table: "Contracts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_TenantUserId",
                table: "Contracts",
                column: "TenantUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractTenants_Users_TenantId",
                table: "ContractTenants",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_RecordedBy",
                table: "Expenses",
                column: "RecordedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_CreatedBy",
                table: "Invoices",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceIssues_Users_AssignedTo",
                table: "MaintenanceIssues",
                column: "AssignedTo",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceIssues_Users_ReportedBy",
                table: "MaintenanceIssues",
                column: "ReportedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MeterReadings_Services_ServiceId",
                table: "MeterReadings",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MeterReadings_Users_CreatedBy",
                table: "MeterReadings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Contracts_ContractId",
                table: "Payments",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_RecordedBy",
                table: "Payments",
                column: "RecordedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomReservations_Rooms_RoomId",
                table: "RoomReservations",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomReservations_Users_TenantUserId",
                table: "RoomReservations",
                column: "TenantUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_Users_OwnerId",
                table: "Buildings");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_RoomReservations_ReservationId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Rooms_RoomId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_CreatedBy",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_TenantUserId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractTenants_Users_TenantId",
                table: "ContractTenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_RecordedBy",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_CreatedBy",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceIssues_Users_AssignedTo",
                table: "MaintenanceIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceIssues_Users_ReportedBy",
                table: "MaintenanceIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_MeterReadings_Services_ServiceId",
                table: "MeterReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_MeterReadings_Users_CreatedBy",
                table: "MeterReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Contracts_ContractId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_RecordedBy",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomReservations_Rooms_RoomId",
                table: "RoomReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomReservations_Users_TenantUserId",
                table: "RoomReservations");

            migrationBuilder.DropTable(
                name: "InvoiceDetails");

            migrationBuilder.DropTable(
                name: "RoomServices");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Users_Phone",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenantProfiles",
                table: "TenantProfiles");

            migrationBuilder.DropIndex(
                name: "IX_TenantProfiles_UserId",
                table: "TenantProfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaffAssignments",
                table: "StaffAssignments");

            migrationBuilder.DropIndex(
                name: "IX_StaffAssignments_StaffId",
                table: "StaffAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_BuildingId_RoomNumber",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_CreatedBy",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_RoomId_ServiceId_BillingYear_BillingMonth",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_ServiceId",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_BillingYear_BillingMonth",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BuildingId",
                table: "Expenses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ContractTenants",
                table: "ContractTenants");

            migrationBuilder.DropIndex(
                name: "IX_ContractTenants_ContractId_TenantId",
                table: "ContractTenants");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_CreatedBy",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ReservationId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_OwnerId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "IdCardBack",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "IssuedDate",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "RoomNumber",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "RefundDate",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Consumption",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "CurrentReading",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "PreviousReading",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MaintenanceIssues");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PenaltyAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RoomAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ServiceAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ContractTenants");

            migrationBuilder.DropColumn(
                name: "MoveInDate",
                table: "ContractTenants");

            migrationBuilder.DropColumn(
                name: "MoveOutDate",
                table: "ContractTenants");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "RoomPrice",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Buildings");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Users",
                newName: "PhoneNumber");

            migrationBuilder.RenameColumn(
                name: "PermanentAddress",
                table: "TenantProfiles",
                newName: "Occupation");

            migrationBuilder.RenameColumn(
                name: "IssuedPlace",
                table: "TenantProfiles",
                newName: "IdCardFrontUrl");

            migrationBuilder.RenameColumn(
                name: "IdCardFront",
                table: "TenantProfiles",
                newName: "IdCardBackUrl");

            migrationBuilder.RenameColumn(
                name: "Floor",
                table: "Rooms",
                newName: "FloorNumber");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "RoomReservations",
                newName: "ExpectedMoveInDate");

            migrationBuilder.RenameColumn(
                name: "TenantUserId",
                table: "RoomReservations",
                newName: "BuildingId");

            migrationBuilder.RenameIndex(
                name: "IX_RoomReservations_TenantUserId",
                table: "RoomReservations",
                newName: "IX_RoomReservations_BuildingId");

            migrationBuilder.RenameColumn(
                name: "ReferenceCode",
                table: "Payments",
                newName: "TransferDetails");

            migrationBuilder.RenameColumn(
                name: "RecordedBy",
                table: "Payments",
                newName: "ReceiverId");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "Payments",
                newName: "ReceiptImageUrl");

            migrationBuilder.RenameColumn(
                name: "ContractId",
                table: "Payments",
                newName: "PayerId");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_RecordedBy",
                table: "Payments",
                newName: "IX_Payments_ReceiverId");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_ContractId",
                table: "Payments",
                newName: "IX_Payments_PayerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Notifications",
                newName: "ReceiverId");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "Notifications",
                newName: "Content");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                newName: "IX_Notifications_ReceiverId");

            migrationBuilder.RenameColumn(
                name: "ServiceId",
                table: "MeterReadings",
                newName: "ContractId");

            migrationBuilder.RenameColumn(
                name: "DateRead",
                table: "MeterReadings",
                newName: "ReadingDate");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "MaintenanceIssues",
                newName: "ReportedAt");

            migrationBuilder.RenameColumn(
                name: "ReportedBy",
                table: "MaintenanceIssues",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "MaintenanceIssues",
                newName: "ResolutionNotes");

            migrationBuilder.RenameColumn(
                name: "AssignedTo",
                table: "MaintenanceIssues",
                newName: "HandledById");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceIssues_ReportedBy",
                table: "MaintenanceIssues",
                newName: "IX_MaintenanceIssues_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceIssues_AssignedTo",
                table: "MaintenanceIssues",
                newName: "IX_MaintenanceIssues_HandledById");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "Invoices",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_CreatedBy",
                table: "Invoices",
                newName: "IX_Invoices_TenantId");

            migrationBuilder.RenameColumn(
                name: "RecordedBy",
                table: "Expenses",
                newName: "RecordedById");

            migrationBuilder.RenameColumn(
                name: "ReceiptUrl",
                table: "Expenses",
                newName: "ReceiptImageUrl");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_RecordedBy",
                table: "Expenses",
                newName: "IX_Expenses_RecordedById");

            migrationBuilder.RenameColumn(
                name: "TerminationReason",
                table: "Contracts",
                newName: "ContractFileUrl");

            migrationBuilder.RenameColumn(
                name: "TerminationDate",
                table: "Contracts",
                newName: "DepositRefundDate");

            migrationBuilder.RenameColumn(
                name: "TenantUserId",
                table: "Contracts",
                newName: "RepresentativeId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_TenantUserId",
                table: "Contracts",
                newName: "IX_Contracts_RepresentativeId");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Buildings",
                newName: "SharedAmenities");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "IdentityCard",
                table: "TenantProfiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "TenantProfiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hometown",
                table: "TenantProfiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Images",
                table: "Rooms",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Area",
                table: "Rooms",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)");

            migrationBuilder.AddColumn<string>(
                name: "Amenities",
                table: "Rooms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseRent",
                table: "Rooms",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "Rooms",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RoomCode",
                table: "Rooms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "RoomReservations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "RoomReservations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ReservationFee",
                table: "RoomReservations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "Payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Payments",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<Guid>(
                name: "SenderId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MeterReadings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "NewElectricityIndex",
                table: "MeterReadings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NewWaterIndex",
                table: "MeterReadings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OldElectricityIndex",
                table: "MeterReadings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OldWaterIndex",
                table: "MeterReadings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordedById",
                table: "MeterReadings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "MaintenanceIssues",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<DateTime>(
                name: "HandledAt",
                table: "MaintenanceIssues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Images",
                table: "MaintenanceIssues",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IssueType",
                table: "MaintenanceIssues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "MaintenanceIssues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<Guid>(
                name: "BuildingId",
                table: "Invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityCharge",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ElectricityUsage",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceCode",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAmount",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomCharge",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ServiceFees",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "WaterCharge",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WaterUsage",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Expenses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Expenses",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DepositAmount",
                table: "Contracts",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "ContractCode",
                table: "Contracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityRate",
                table: "Contracts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InitialElectricityIndex",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InitialWaterIndex",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCycle",
                table: "Contracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PaymentDate",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedDepositAmount",
                table: "Contracts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RentAmount",
                table: "Contracts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ServiceFees",
                table: "Contracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "WaterRate",
                table: "Contracts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AddressCity",
                table: "Buildings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressDistrict",
                table: "Buildings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressNumber",
                table: "Buildings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressStreet",
                table: "Buildings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressWard",
                table: "Buildings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Images",
                table: "Buildings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenantProfiles",
                table: "TenantProfiles",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaffAssignments",
                table: "StaffAssignments",
                columns: new[] { "StaffId", "BuildingId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ContractTenants",
                table: "ContractTenants",
                columns: new[] { "ContractId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAssignments_BuildingId",
                table: "StaffAssignments",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BuildingId_RoomCode",
                table: "Rooms",
                columns: new[] { "BuildingId", "RoomCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SenderId",
                table: "Notifications",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_ContractId_BillingMonth_BillingYear",
                table: "MeterReadings",
                columns: new[] { "ContractId", "BillingMonth", "BillingYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_RecordedById",
                table: "MeterReadings",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_RoomId",
                table: "MeterReadings",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BuildingId",
                table: "Invoices",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId",
                table: "Invoices",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceCode",
                table: "Invoices",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BuildingId_ExpenseDate",
                table: "Expenses",
                columns: new[] { "BuildingId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_RoomId",
                table: "Expenses",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractCode",
                table: "Contracts",
                column: "ContractCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Rooms_RoomId",
                table: "Contracts",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_RepresentativeId",
                table: "Contracts",
                column: "RepresentativeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractTenants_Users_TenantId",
                table: "ContractTenants",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Rooms_RoomId",
                table: "Expenses",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_RecordedById",
                table: "Expenses",
                column: "RecordedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Buildings_BuildingId",
                table: "Invoices",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_TenantId",
                table: "Invoices",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceIssues_Users_HandledById",
                table: "MaintenanceIssues",
                column: "HandledById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceIssues_Users_TenantId",
                table: "MaintenanceIssues",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MeterReadings_Contracts_ContractId",
                table: "MeterReadings",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MeterReadings_Users_RecordedById",
                table: "MeterReadings",
                column: "RecordedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_ReceiverId",
                table: "Notifications",
                column: "ReceiverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_SenderId",
                table: "Notifications",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_PayerId",
                table: "Payments",
                column: "PayerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_ReceiverId",
                table: "Payments",
                column: "ReceiverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomReservations_Buildings_BuildingId",
                table: "RoomReservations",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomReservations_Rooms_RoomId",
                table: "RoomReservations",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

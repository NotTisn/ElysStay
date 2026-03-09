using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignToSpec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractTenants_Users_TenantId",
                table: "ContractTenants");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceIssues_Rooms_RoomId",
                table: "MaintenanceIssues");

            migrationBuilder.DropIndex(
                name: "IX_Users_Phone",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TenantProfiles_IdentityCard",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "IdentityCard",
                table: "TenantProfiles",
                newName: "IdNumber");

            migrationBuilder.RenameColumn(
                name: "IdCardFront",
                table: "TenantProfiles",
                newName: "IdFrontUrl");

            migrationBuilder.RenameColumn(
                name: "IdCardBack",
                table: "TenantProfiles",
                newName: "IdBackUrl");

            migrationBuilder.RenameColumn(
                name: "RefundDate",
                table: "RoomReservations",
                newName: "RefundedAt");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "RoomReservations",
                newName: "RefundNote");

            migrationBuilder.RenameColumn(
                name: "CancelReason",
                table: "RoomReservations",
                newName: "Note");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Payments",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "PaymentDate",
                table: "Payments",
                newName: "PaidAt");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "MaintenanceIssues",
                newName: "ImageUrls");

            migrationBuilder.RenameColumn(
                name: "RoomAmount",
                table: "Invoices",
                newName: "RentAmount");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "ContractTenants",
                newName: "TenantUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ContractTenants_TenantId",
                table: "ContractTenants",
                newName: "IX_ContractTenants_TenantUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ContractTenants_ContractId_TenantId",
                table: "ContractTenants",
                newName: "IX_ContractTenants_ContractId_TenantUserId");

            migrationBuilder.RenameColumn(
                name: "TerminationReason",
                table: "Contracts",
                newName: "TerminationNote");

            migrationBuilder.RenameColumn(
                name: "RoomPrice",
                table: "Contracts",
                newName: "MonthlyRent");

            migrationBuilder.RenameColumn(
                name: "TotalRooms",
                table: "Buildings",
                newName: "TotalFloors");

            migrationBuilder.AddColumn<string>(
                name: "KeycloakId",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "IssuedDate",
                table: "TenantProfiles",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "TenantProfiles",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PriceUpdatedAt",
                table: "Services",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<decimal>(
                name: "PreviousUnitPrice",
                table: "Services",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Rooms",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "RoomReservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MeterReadings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "MeterReadings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<Guid>(
                name: "RoomId",
                table: "MaintenanceIssues",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DueDate",
                table: "Invoices",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ExpenseDate",
                table: "Expenses",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MoveOutDate",
                table: "ContractTenants",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MoveInDate",
                table: "ContractTenants",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "IsMainTenant",
                table: "ContractTenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "TerminationDate",
                table: "Contracts",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "StartDate",
                table: "Contracts",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EndDate",
                table: "Contracts",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateOnly>(
                name: "MoveInDate",
                table: "Contracts",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "InvoiceDueDay",
                table: "Buildings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Users_KeycloakId",
                table: "Users",
                column: "KeycloakId",
                unique: true,
                filter: "\"KeycloakId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true,
                filter: "\"Phone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantProfiles_IdNumber",
                table: "TenantProfiles",
                column: "IdNumber",
                unique: true,
                filter: "\"IdNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_RoomId",
                table: "Expenses",
                column: "RoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractTenants_Users_TenantUserId",
                table: "ContractTenants",
                column: "TenantUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Rooms_RoomId",
                table: "Expenses",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceIssues_Rooms_RoomId",
                table: "MaintenanceIssues",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractTenants_Users_TenantUserId",
                table: "ContractTenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Rooms_RoomId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceIssues_Rooms_RoomId",
                table: "MaintenanceIssues");

            migrationBuilder.DropIndex(
                name: "IX_Users_KeycloakId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Phone",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TenantProfiles_IdNumber",
                table: "TenantProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_RoomId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "KeycloakId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "RoomReservations");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "IsMainTenant",
                table: "ContractTenants");

            migrationBuilder.DropColumn(
                name: "MoveInDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "InvoiceDueDay",
                table: "Buildings");

            migrationBuilder.RenameColumn(
                name: "IdNumber",
                table: "TenantProfiles",
                newName: "IdentityCard");

            migrationBuilder.RenameColumn(
                name: "IdFrontUrl",
                table: "TenantProfiles",
                newName: "IdCardFront");

            migrationBuilder.RenameColumn(
                name: "IdBackUrl",
                table: "TenantProfiles",
                newName: "IdCardBack");

            migrationBuilder.RenameColumn(
                name: "RefundedAt",
                table: "RoomReservations",
                newName: "RefundDate");

            migrationBuilder.RenameColumn(
                name: "RefundNote",
                table: "RoomReservations",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "RoomReservations",
                newName: "CancelReason");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Payments",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "PaidAt",
                table: "Payments",
                newName: "PaymentDate");

            migrationBuilder.RenameColumn(
                name: "ImageUrls",
                table: "MaintenanceIssues",
                newName: "ImageUrl");

            migrationBuilder.RenameColumn(
                name: "RentAmount",
                table: "Invoices",
                newName: "RoomAmount");

            migrationBuilder.RenameColumn(
                name: "TenantUserId",
                table: "ContractTenants",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ContractTenants_TenantUserId",
                table: "ContractTenants",
                newName: "IX_ContractTenants_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ContractTenants_ContractId_TenantUserId",
                table: "ContractTenants",
                newName: "IX_ContractTenants_ContractId_TenantId");

            migrationBuilder.RenameColumn(
                name: "TerminationNote",
                table: "Contracts",
                newName: "TerminationReason");

            migrationBuilder.RenameColumn(
                name: "MonthlyRent",
                table: "Contracts",
                newName: "RoomPrice");

            migrationBuilder.RenameColumn(
                name: "TotalFloors",
                table: "Buildings",
                newName: "TotalRooms");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "IssuedDate",
                table: "TenantProfiles",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "TenantProfiles",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PriceUpdatedAt",
                table: "Services",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PreviousUnitPrice",
                table: "Services",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "RoomReservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoomId",
                table: "MaintenanceIssues",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueDate",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpenseDate",
                table: "Expenses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "MoveOutDate",
                table: "ContractTenants",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "MoveInDate",
                table: "ContractTenants",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TerminationDate",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantProfiles_IdentityCard",
                table: "TenantProfiles",
                column: "IdentityCard",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractTenants_Users_TenantId",
                table: "ContractTenants",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceIssues_Rooms_RoomId",
                table: "MaintenanceIssues",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

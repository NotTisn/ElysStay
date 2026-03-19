using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixInvoiceUniqueConstraintForVoids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_BillingYear_BillingMonth",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_BillingYear_BillingMonth",
                table: "Invoices",
                columns: new[] { "ContractId", "BillingYear", "BillingMonth" },
                unique: true,
                filter: "\"Status\" <> 'Void'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_BillingYear_BillingMonth",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_BillingYear_BillingMonth",
                table: "Invoices",
                columns: new[] { "ContractId", "BillingYear", "BillingMonth" },
                unique: true);
        }
    }
}

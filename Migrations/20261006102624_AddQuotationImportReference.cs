using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationImportReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerCode",
                table: "PurchaseOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuotationNumber",
                table: "PurchaseOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuotationRevision",
                table: "PurchaseOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RfqNumber",
                table: "PurchaseOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSystem",
                table: "PurchaseOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SourceSystem_QuotationNumber_QuotationRevision",
                table: "PurchaseOrders",
                columns: new[] { "SourceSystem", "QuotationNumber", "QuotationRevision" },
                unique: true,
                filter: "[SourceSystem] IS NOT NULL AND [QuotationNumber] IS NOT NULL AND [QuotationRevision] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_SourceSystem_QuotationNumber_QuotationRevision",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CustomerCode",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "QuotationNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "QuotationRevision",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RfqNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceSystem",
                table: "PurchaseOrders");
        }
    }
}

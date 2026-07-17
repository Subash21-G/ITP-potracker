using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POTracker.Migrations
{
    /// <inheritdoc />
    public partial class FixVyaparInvoiceImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_VyaparInvoiceImportRows_BatchId",
                table: "VyaparInvoiceImportRows",
                column: "BatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_VyaparInvoiceImportRows_VyaparInvoiceImportBatches_BatchId",
                table: "VyaparInvoiceImportRows",
                column: "BatchId",
                principalTable: "VyaparInvoiceImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VyaparInvoiceImportRows_VyaparInvoiceImportBatches_BatchId",
                table: "VyaparInvoiceImportRows");

            migrationBuilder.DropIndex(
                name: "IX_VyaparInvoiceImportRows_BatchId",
                table: "VyaparInvoiceImportRows");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "VyaparInvoiceImportRows");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "VyaparInvoiceImportRows");
        }
    }
}

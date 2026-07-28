using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POTracker.Migrations
{
    /// <inheritdoc />
    public partial class FixPurchaserRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Purchasers_PurchaserId1",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PurchaserId1",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PurchaserId1",
                table: "PurchaseOrders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchaserId1",
                table: "PurchaseOrders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PurchaserId1",
                table: "PurchaseOrders",
                column: "PurchaserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Purchasers_PurchaserId1",
                table: "PurchaseOrders",
                column: "PurchaserId1",
                principalTable: "Purchasers",
                principalColumn: "Id");
        }
    }
}

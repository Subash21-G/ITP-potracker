using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfImportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerContactPerson",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfFilePath",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dep",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DrawingNo",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceUnit",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalValue",
                table: "Materials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValueOfGoods",
                table: "Materials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyerContactPerson",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PdfFilePath",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReceivedDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Dep",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "DrawingNo",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "PriceUnit",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "TotalValue",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ValueOfGoods",
                table: "Materials");
        }
    }
}

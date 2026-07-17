using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POTracker.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMaterialFullFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedDashboard",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Materials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Materials",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DrawingFilePath",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DrawingUpdatedDate",
                table: "Materials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DrawingVersion",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MachiningType",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialProcessTime",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PDFFilePath",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PODate",
                table: "Materials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessTime",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RMProcess",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivingDate",
                table: "Materials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StockQty",
                table: "Materials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedDashboard",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "DrawingFilePath",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "DrawingUpdatedDate",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "DrawingVersion",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "MachiningType",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "MaterialProcessTime",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "PDFFilePath",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "PODate",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ProcessTime",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "RMProcess",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ReceivingDate",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "StockQty",
                table: "Materials");
        }
    }
}

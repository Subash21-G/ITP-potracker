using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterSheetPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MasterSheetProcessOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterSheetProcessOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterSheetRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialNo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DrawingVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FinishSize = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequiredRmSize = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterSheetRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterSheetProcessSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MasterSheetRecordId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ProcessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterSheetProcessSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterSheetProcessSteps_MasterSheetRecords_MasterSheetRecordId",
                        column: x => x.MasterSheetRecordId,
                        principalTable: "MasterSheetRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterSheetProcessOptions_CreatedDate",
                table: "MasterSheetProcessOptions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_MasterSheetProcessSteps_MasterSheetRecordId_Sequence",
                table: "MasterSheetProcessSteps",
                columns: new[] { "MasterSheetRecordId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterSheetRecords_MaterialNo",
                table: "MasterSheetRecords",
                column: "MaterialNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterSheetProcessOptions");

            migrationBuilder.DropTable(
                name: "MasterSheetProcessSteps");

            migrationBuilder.DropTable(
                name: "MasterSheetRecords");
        }
    }
}

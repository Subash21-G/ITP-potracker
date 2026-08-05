using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PurchaserId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PendingInvoiceItems_InvoiceId",
                table: "PendingInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_Dispatches_MaterialId",
                table: "Dispatches");

            migrationBuilder.AlterColumn<string>(
                name: "PONumber",
                table: "InvoiceItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "MaterialNo",
                table: "InvoiceItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "LineItem",
                table: "InvoiceItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DispatchNo",
                table: "Dispatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "AppUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "AppUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AppUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // SQL Server cannot index nvarchar(max) columns.
            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PONumber",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "MaterialNumber",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_VyaparInvoiceImportRows_InvoiceNumber_Status",
                table: "VyaparInvoiceImportRows",
                columns: new[] { "InvoiceNumber", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VyaparInvoiceImportRows_PONumber_MaterialNumber_LineItem",
                table: "VyaparInvoiceImportRows",
                columns: new[] { "PONumber", "MaterialNumber", "LineItem" });

            migrationBuilder.CreateIndex(
                name: "IX_VyaparInvoiceImportBatches_ImportDate",
                table: "VyaparInvoiceImportBatches",
                column: "ImportDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PurchaserId_PODate",
                table: "PurchaseOrders",
                columns: new[] { "PurchaserId", "PODate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ReceivedDate",
                table: "PurchaseOrders",
                column: "ReceivedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Status_CreatedDate",
                table: "PurchaseOrders",
                columns: new[] { "Status", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingInvoiceItems_InvoiceId_Status",
                table: "PendingInvoiceItems",
                columns: new[] { "InvoiceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingInvoiceItems_PONumber_MaterialNo_LineItem",
                table: "PendingInvoiceItems",
                columns: new[] { "PONumber", "MaterialNo", "LineItem" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingInvoiceItems_Status_CreatedDate",
                table: "PendingInvoiceItems",
                columns: new[] { "Status", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_DispatchStatus_DeliveryDate",
                table: "Materials",
                columns: new[] { "DispatchStatus", "DeliveryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_InvoiceStatus_CreatedDate",
                table: "Materials",
                columns: new[] { "InvoiceStatus", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_MaterialNo",
                table: "Materials",
                column: "MaterialNo");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_ProcessStatus_ProcessStartDate",
                table: "Materials",
                columns: new[] { "ProcessStatus", "ProcessStartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_PurchaseOrderId_MaterialNo_LineItem",
                table: "Materials",
                columns: new[] { "PurchaseOrderId", "MaterialNo", "LineItem" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedDate",
                table: "Invoices",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId_MaterialId_LineItem",
                table: "InvoiceItems",
                columns: new[] { "InvoiceId", "MaterialId", "LineItem" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_PONumber_MaterialNo_LineItem",
                table: "InvoiceItems",
                columns: new[] { "PONumber", "MaterialNo", "LineItem" });

            migrationBuilder.CreateIndex(
                name: "IX_Dispatches_DispatchNo",
                table: "Dispatches",
                column: "DispatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_Dispatches_MaterialId_DispatchDate",
                table: "Dispatches",
                columns: new[] { "MaterialId", "DispatchDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_UserName",
                table: "AppUsers",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VyaparInvoiceImportRows_InvoiceNumber_Status",
                table: "VyaparInvoiceImportRows");

            migrationBuilder.DropIndex(
                name: "IX_VyaparInvoiceImportRows_PONumber_MaterialNumber_LineItem",
                table: "VyaparInvoiceImportRows");

            migrationBuilder.DropIndex(
                name: "IX_VyaparInvoiceImportBatches_ImportDate",
                table: "VyaparInvoiceImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PurchaserId_PODate",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_ReceivedDate",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_Status_CreatedDate",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PendingInvoiceItems_InvoiceId_Status",
                table: "PendingInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_PendingInvoiceItems_PONumber_MaterialNo_LineItem",
                table: "PendingInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_PendingInvoiceItems_Status_CreatedDate",
                table: "PendingInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_Materials_DispatchStatus_DeliveryDate",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_InvoiceStatus_CreatedDate",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_MaterialNo",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_ProcessStatus_ProcessStartDate",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_PurchaseOrderId_MaterialNo_LineItem",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CreatedDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_InvoiceId_MaterialId_LineItem",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_PONumber_MaterialNo_LineItem",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_Dispatches_DispatchNo",
                table: "Dispatches");

            migrationBuilder.DropIndex(
                name: "IX_Dispatches_MaterialId_DispatchDate",
                table: "Dispatches");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_UserName",
                table: "AppUsers");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "PONumber",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "MaterialNumber",
                table: "VyaparInvoiceImportRows",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "PONumber",
                table: "InvoiceItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "MaterialNo",
                table: "InvoiceItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "LineItem",
                table: "InvoiceItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "DispatchNo",
                table: "Dispatches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PurchaserId",
                table: "PurchaseOrders",
                column: "PurchaserId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingInvoiceItems_InvoiceId",
                table: "PendingInvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispatches_MaterialId",
                table: "Dispatches",
                column: "MaterialId");
        }
    }
}

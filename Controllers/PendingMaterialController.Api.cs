using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class PendingMaterialController
    {
        [HttpGet("~/api/pending-materials")]
        public async Task<IActionResult> GetPendingMaterialsApi(
            string? search,
            string? poNumber,
            string? handler,
            string? assignedDashboard,
            string? processStatus,
            string? invoiceStatus,
            string? dispatchStatus,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            DateTime? deliveryDateFrom,
            DateTime? deliveryDateTo,
            bool overdueOnly = false,
            bool dueThisWeekOnly = false,
            int page = 1,
            int pageSize = 25)
        {
            if (!IsLoggedIn())
            {
                return Unauthorized(new
                {
                    message =
                        "Your session has expired. Sign in again."
                });
            }

            page = Math.Max(1, page);
            pageSize = NormalizePageSize(pageSize);

            var baseQuery =
                CreatePendingQuery();

            var dropdownData =
                await LoadDropdownDataAsync(baseQuery);

            var filteredQuery =
                ApplyFilters(
                    baseQuery,
                    search,
                    poNumber,
                    handler,
                    assignedDashboard,
                    processStatus,
                    invoiceStatus,
                    dispatchStatus,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo,
                    deliveryDateFrom,
                    deliveryDateTo,
                    overdueOnly,
                    dueThisWeekOnly);

            var summary =
                await BuildSummaryAsync(filteredQuery);

            var totalRecords =
                summary.TotalPendingMaterials;

            var totalPages =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        totalRecords /
                        (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var today =
                DateTime.Today;

            var materials =
                await filteredQuery
                    .OrderBy(material =>
                        !material.DeliveryDate.HasValue
                            ? 2
                            : material.DeliveryDate < today
                                ? 0
                                : 1)
                    .ThenBy(material =>
                        material.DeliveryDate)
                    .ThenByDescending(material =>
                        material.Id)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(pageSize)
                    .ToListAsync();

            var items =
                materials
                    .Select(MapRow)
                    .Select(MapPendingMaterialApiItem)
                    .ToList();

            return Ok(
                new PendingMaterialsResponseDto
                {
                    Items = items,

                    Summary =
                        new PendingMaterialSummaryDto
                        {
                            TotalPendingMaterials =
                                summary.TotalPendingMaterials,

                            OverdueMaterials =
                                summary.OverdueMaterials,

                            DueTodayMaterials =
                                summary.DueTodayMaterials,

                            DueThisWeekMaterials =
                                summary.DueThisWeekMaterials,

                            WithoutDeliveryDate =
                                summary.WithoutDeliveryDate,

                            TotalPOQuantity =
                                summary.TotalPOQuantity,

                            TotalInvoiceQuantity =
                                summary.TotalInvoiceQuantity,

                            TotalInvoiceBalanceQuantity =
                                summary.TotalInvoiceBalanceQuantity,

                            TotalDispatchQuantity =
                                summary.TotalDispatchQuantity,

                            TotalPendingDispatchQuantity =
                                summary.TotalPendingDispatchQuantity,

                            TotalPendingValue =
                                summary.TotalPendingValue,

                            AssignmentSummary =
                                summary.AssignmentSummary
                                    .Select(item =>
                                        new PendingMaterialAssignmentDto
                                        {
                                            AssignedDashboard =
                                                item.AssignedDashboard,

                                            MaterialCount =
                                                item.MaterialCount,

                                            PendingQuantity =
                                                item.PendingQuantity,

                                            OverdueCount =
                                                item.OverdueCount
                                        })
                                    .ToList()
                        },

                    Filters =
                        new PendingMaterialFilterDataDto
                        {
                            Handlers =
                                MapFilterOptions(
                                    dropdownData.Handlers),

                            AssignedDashboards =
                                MapFilterOptions(
                                    dropdownData.AssignedDashboards),

                            ProcessStatuses =
                                MapFilterOptions(
                                    dropdownData.ProcessStatuses),

                            InvoiceStatuses =
                                MapFilterOptions(
                                    dropdownData.InvoiceStatuses),

                            DispatchStatuses =
                                MapFilterOptions(
                                    dropdownData.DispatchStatuses)
                        },

                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    TotalPages = totalPages
                });
        }

        /// <summary>
        /// Exports all filtered Pending Materials used by the Angular screen.
        /// </summary>
        [HttpGet("~/api/pending-materials/export-excel")]
        [Produces(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult>
            ExportPendingMaterialsExcelApi(
                string? search,
                string? poNumber,
                string? handler,
                string? assignedDashboard,
                string? processStatus,
                string? invoiceStatus,
                string? dispatchStatus,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                bool overdueOnly = false,
                bool dueThisWeekOnly = false,
                CancellationToken cancellationToken = default)
        {
            if (!IsLoggedIn())
            {
                return Unauthorized(new
                {
                    message =
                        "Your session has expired. Sign in again."
                });
            }

            var materials =
                await ApplyFilters(
                        CreatePendingQuery(),
                        search,
                        poNumber,
                        handler,
                        assignedDashboard,
                        processStatus,
                        invoiceStatus,
                        dispatchStatus,
                        poDateFrom,
                        poDateTo,
                        poReceivedDateFrom,
                        poReceivedDateTo,
                        deliveryDateFrom,
                        deliveryDateTo,
                        overdueOnly,
                        dueThisWeekOnly)
                    .OrderBy(material =>
                        !material.DeliveryDate.HasValue
                            ? 2
                            : material.DeliveryDate.Value.Date <
                              DateTime.Today
                                ? 0
                                : 1)
                    .ThenBy(material =>
                        material.DeliveryDate)
                    .ThenByDescending(material =>
                        material.Id)
                    .ToListAsync(cancellationToken);

            var rows = materials
                .Select(MapRow)
                .ToList();

            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Pending Materials");

            WriteExcel(
                worksheet,
                rows);

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Pending_Materials_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        private PendingMaterialItemDto
            MapPendingMaterialApiItem(
                PendingMaterialRowViewModel row)
        {
            return new PendingMaterialItemDto
            {
                MaterialId = row.MaterialId,
                PurchaseOrderId = row.PurchaseOrderId,
                PONumber = row.PONumber,
                Handler = row.Handler,
                PODate = row.PODate,
                POReceivedDate = row.POReceivedDate,
                LineItem = row.LineItem,
                MaterialNo = row.MaterialNo,
                HsnSacCode = row.HsnSacCode,
                ShortDescription = row.ShortDescription,
                BriefDescription = row.BriefDescription,
                Unit = row.Unit,
                Department = row.Department,
                DrawingNo = row.DrawingNo,
                DrawingVersion = row.DrawingVersion,
                POQuantity = row.POQuantity,
                InvoiceQuantity = row.InvoiceQuantity,
                InvoiceBalanceQuantity = row.InvoiceBalanceQuantity,
                DispatchQuantity = row.DispatchQuantity,
                PendingDispatchQuantity = row.PendingDispatchQuantity,
                StockQuantity = row.StockQuantity,
                UnitCost = row.UnitCost,
                TotalValue = row.TotalValue,
                DeliveryDate = row.DeliveryDate,
                ProcessStartDate = row.ProcessStartDate,
                AlertDate = row.AlertDate,
                ProcessStatus = row.ProcessStatus,
                InvoiceStatus = row.InvoiceStatus,
                DispatchStatus = row.DispatchStatus,
                AssignedDashboard = row.AssignedDashboard,
                Remarks = row.Remarks,
                PdfFilePath =
                    _documentReferences.ToDownloadUrl(
                        row.PdfFilePath,
                        "pdfs"),
                DrawingFilePath =
                    _documentReferences.ToDownloadUrl(
                        row.DrawingFilePath,
                        "drawings"),
                CompletionPercentage = row.CompletionPercentage,
                OverdueDays = row.OverdueDays,
                IsOverdue = row.IsOverdue,
                IsDueToday = row.IsDueToday,
                IsDueThisWeek = row.IsDueThisWeek,
                DueStatus = row.DueStatus
            };
        }

        private static List<PendingMaterialFilterOptionDto>
            MapFilterOptions(
                IEnumerable<
                    PendingMaterialFilterOptionViewModel
                > options)
        {
            return options
                .Select(option =>
                    new PendingMaterialFilterOptionDto
                    {
                        Value = option.Value,
                        Text = option.Text,
                        Count = option.Count
                    })
                .ToList();
        }
    }
}

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

            string? dueStatus = null,
            string? columnPoNumber = null,
            string? columnHandler = null,
            string? columnMaterialNo = null,
            string? columnLineItem = null,
            string? columnDescription = null,
            decimal? minPoQuantity = null,
            decimal? maxPoQuantity = null,
            decimal? minInvoiceQuantity = null,
            decimal? maxInvoiceQuantity = null,
            decimal? minInvoiceBalanceQuantity = null,
            decimal? maxInvoiceBalanceQuantity = null,
            decimal? minDispatchQuantity = null,
            decimal? maxDispatchQuantity = null,
            decimal? minPendingDispatchQuantity = null,
            decimal? maxPendingDispatchQuantity = null,
            decimal? minCompletionPercentage = null,
            decimal? maxCompletionPercentage = null,
            string? columnProcessStatus = null,
            string? columnInvoiceStatus = null,
            string? columnDispatchStatus = null,
            DateTime? columnDeliveryDateFrom = null,
            DateTime? columnDeliveryDateTo = null,
            string? columnAssignedDashboard = null,

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

            filteredQuery =
                ApplyHeaderFilters(
                    filteredQuery,
                    dueStatus,
                    columnPoNumber,
                    columnHandler,
                    columnMaterialNo,
                    columnLineItem,
                    columnDescription,
                    minPoQuantity,
                    maxPoQuantity,
                    minInvoiceQuantity,
                    maxInvoiceQuantity,
                    minInvoiceBalanceQuantity,
                    maxInvoiceBalanceQuantity,
                    minDispatchQuantity,
                    maxDispatchQuantity,
                    minPendingDispatchQuantity,
                    maxPendingDispatchQuantity,
                    minCompletionPercentage,
                    maxCompletionPercentage,
                    columnProcessStatus,
                    columnInvoiceStatus,
                    columnDispatchStatus,
                    columnDeliveryDateFrom,
                    columnDeliveryDateTo,
                    columnAssignedDashboard);

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

                string? dueStatus = null,
                string? columnPoNumber = null,
                string? columnHandler = null,
                string? columnMaterialNo = null,
                string? columnLineItem = null,
                string? columnDescription = null,
                decimal? minPoQuantity = null,
                decimal? maxPoQuantity = null,
                decimal? minInvoiceQuantity = null,
                decimal? maxInvoiceQuantity = null,
                decimal? minInvoiceBalanceQuantity = null,
                decimal? maxInvoiceBalanceQuantity = null,
                decimal? minDispatchQuantity = null,
                decimal? maxDispatchQuantity = null,
                decimal? minPendingDispatchQuantity = null,
                decimal? maxPendingDispatchQuantity = null,
                decimal? minCompletionPercentage = null,
                decimal? maxCompletionPercentage = null,
                string? columnProcessStatus = null,
                string? columnInvoiceStatus = null,
                string? columnDispatchStatus = null,
                DateTime? columnDeliveryDateFrom = null,
                DateTime? columnDeliveryDateTo = null,
                string? columnAssignedDashboard = null,

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

            var filteredQuery =
                ApplyFilters(
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
                    dueThisWeekOnly);

            filteredQuery =
                ApplyHeaderFilters(
                    filteredQuery,
                    dueStatus,
                    columnPoNumber,
                    columnHandler,
                    columnMaterialNo,
                    columnLineItem,
                    columnDescription,
                    minPoQuantity,
                    maxPoQuantity,
                    minInvoiceQuantity,
                    maxInvoiceQuantity,
                    minInvoiceBalanceQuantity,
                    maxInvoiceBalanceQuantity,
                    minDispatchQuantity,
                    maxDispatchQuantity,
                    minPendingDispatchQuantity,
                    maxPendingDispatchQuantity,
                    minCompletionPercentage,
                    maxCompletionPercentage,
                    columnProcessStatus,
                    columnInvoiceStatus,
                    columnDispatchStatus,
                    columnDeliveryDateFrom,
                    columnDeliveryDateTo,
                    columnAssignedDashboard);

            var materials =
                await filteredQuery
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

            var rows =
                materials
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

        private static IQueryable<POTracker.Models.Material>
            ApplyHeaderFilters(
                IQueryable<POTracker.Models.Material> query,
                string? dueStatus,
                string? columnPoNumber,
                string? columnHandler,
                string? columnMaterialNo,
                string? columnLineItem,
                string? columnDescription,
                decimal? minPoQuantity,
                decimal? maxPoQuantity,
                decimal? minInvoiceQuantity,
                decimal? maxInvoiceQuantity,
                decimal? minInvoiceBalanceQuantity,
                decimal? maxInvoiceBalanceQuantity,
                decimal? minDispatchQuantity,
                decimal? maxDispatchQuantity,
                decimal? minPendingDispatchQuantity,
                decimal? maxPendingDispatchQuantity,
                decimal? minCompletionPercentage,
                decimal? maxCompletionPercentage,
                string? columnProcessStatus,
                string? columnInvoiceStatus,
                string? columnDispatchStatus,
                DateTime? columnDeliveryDateFrom,
                DateTime? columnDeliveryDateTo,
                string? columnAssignedDashboard)
        {
            var today =
                DateTime.Today;

            var weekEnd =
                today.AddDays(7);

            if (!string.IsNullOrWhiteSpace(dueStatus))
            {
                var value =
                    dueStatus.Trim();

                if (value == "No Delivery Date")
                {
                    query =
                        query.Where(x =>
                            !x.DeliveryDate.HasValue);
                }
                else if (value == "Overdue")
                {
                    query =
                        query.Where(x =>
                            x.DeliveryDate.HasValue &&
                            x.DeliveryDate.Value.Date < today &&
                            x.PendingQty > 0);
                }
                else if (value == "Due Today")
                {
                    query =
                        query.Where(x =>
                            x.DeliveryDate.HasValue &&
                            x.DeliveryDate.Value.Date == today &&
                            x.PendingQty > 0);
                }
                else if (value == "Due This Week")
                {
                    query =
                        query.Where(x =>
                            x.DeliveryDate.HasValue &&
                            x.DeliveryDate.Value.Date >= today &&
                            x.DeliveryDate.Value.Date <= weekEnd &&
                            x.PendingQty > 0);
                }
                else if (value == "Upcoming")
                {
                    query =
                        query.Where(x =>
                            x.DeliveryDate.HasValue &&
                            (
                                x.PendingQty <= 0 ||
                                x.DeliveryDate.Value.Date > weekEnd
                            ));
                }
            }

            if (!string.IsNullOrWhiteSpace(columnPoNumber))
            {
                var value =
                    columnPoNumber.Trim();

                query =
                    query.Where(x =>
                        x.PurchaseOrder != null &&
                        x.PurchaseOrder.PONumber.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(columnHandler))
            {
                var value =
                    columnHandler.Trim();

                query =
                    query.Where(x =>
                        x.PurchaseOrder != null &&
                        x.PurchaseOrder.BuyerContactPerson != null &&
                        x.PurchaseOrder.BuyerContactPerson.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(columnMaterialNo))
            {
                var value =
                    columnMaterialNo.Trim();

                query =
                    query.Where(x =>
                        x.MaterialNo != null &&
                        x.MaterialNo.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(columnLineItem))
            {
                var value =
                    columnLineItem.Trim();

                query =
                    query.Where(x =>
                        x.LineItem != null &&
                        x.LineItem.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(columnDescription))
            {
                var value =
                    columnDescription.Trim();

                query =
                    query.Where(x =>
                        (
                            x.ShortDescription != null &&
                            x.ShortDescription.Contains(value)
                        ) ||
                        (
                            x.BriefDescription != null &&
                            x.BriefDescription.Contains(value)
                        ) ||
                        (
                            x.DrawingNo != null &&
                            x.DrawingNo.Contains(value)
                        ));
            }

            if (minPoQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.Qty >= minPoQuantity.Value);
            }

            if (maxPoQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.Qty <= maxPoQuantity.Value);
            }

            if (minInvoiceQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.InvoiceQty >= minInvoiceQuantity.Value);
            }

            if (maxInvoiceQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.InvoiceQty <= maxInvoiceQuantity.Value);
            }

            if (minInvoiceBalanceQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        (
                            x.BalanceQty < 0
                                ? 0
                                : x.BalanceQty
                        ) >= minInvoiceBalanceQuantity.Value);
            }

            if (maxInvoiceBalanceQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        (
                            x.BalanceQty < 0
                                ? 0
                                : x.BalanceQty
                        ) <= maxInvoiceBalanceQuantity.Value);
            }

            if (minDispatchQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.DispatchQty >= minDispatchQuantity.Value);
            }

            if (maxDispatchQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.DispatchQty <= maxDispatchQuantity.Value);
            }

            if (minPendingDispatchQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        (
                            x.PendingQty < 0
                                ? 0
                                : x.PendingQty
                        ) >= minPendingDispatchQuantity.Value);
            }

            if (maxPendingDispatchQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        (
                            x.PendingQty < 0
                                ? 0
                                : x.PendingQty
                        ) <= maxPendingDispatchQuantity.Value);
            }

            if (minCompletionPercentage.HasValue)
            {
                var minimum =
                    Math.Clamp(
                        minCompletionPercentage.Value,
                        0m,
                        100m);

                query =
                    query.Where(x =>
                        (
                            x.Qty <= 0
                                ? 0m
                                : (
                                    (
                                        (
                                            x.InvoiceQty >
                                            x.DispatchQty
                                                ? x.InvoiceQty
                                                : x.DispatchQty
                                        ) /
                                        x.Qty *
                                        100m
                                    ) > 100m
                                        ? 100m
                                        : (
                                            (
                                                x.InvoiceQty >
                                                x.DispatchQty
                                                    ? x.InvoiceQty
                                                    : x.DispatchQty
                                            ) /
                                            x.Qty *
                                            100m
                                        )
                                )
                        ) >= minimum);
            }

            if (maxCompletionPercentage.HasValue)
            {
                var maximum =
                    Math.Clamp(
                        maxCompletionPercentage.Value,
                        0m,
                        100m);

                query =
                    query.Where(x =>
                        (
                            x.Qty <= 0
                                ? 0m
                                : (
                                    (
                                        (
                                            x.InvoiceQty >
                                            x.DispatchQty
                                                ? x.InvoiceQty
                                                : x.DispatchQty
                                        ) /
                                        x.Qty *
                                        100m
                                    ) > 100m
                                        ? 100m
                                        : (
                                            (
                                                x.InvoiceQty >
                                                x.DispatchQty
                                                    ? x.InvoiceQty
                                                    : x.DispatchQty
                                            ) /
                                            x.Qty *
                                            100m
                                        )
                                )
                        ) <= maximum);
            }

            if (!string.IsNullOrWhiteSpace(columnProcessStatus))
            {
                var value =
                    columnProcessStatus.Trim();

                query =
                    query.Where(x =>
                        x.ProcessStatus == value);
            }

            if (!string.IsNullOrWhiteSpace(columnInvoiceStatus))
            {
                var value =
                    columnInvoiceStatus.Trim();

                query =
                    query.Where(x =>
                        x.InvoiceStatus == value);
            }

            if (!string.IsNullOrWhiteSpace(columnDispatchStatus))
            {
                var value =
                    columnDispatchStatus.Trim();

                query =
                    query.Where(x =>
                        x.DispatchStatus == value);
            }

            if (columnDeliveryDateFrom.HasValue)
            {
                var date =
                    columnDeliveryDateFrom.Value.Date;

                query =
                    query.Where(x =>
                        x.DeliveryDate.HasValue &&
                        x.DeliveryDate.Value.Date >= date);
            }

            if (columnDeliveryDateTo.HasValue)
            {
                var date =
                    columnDeliveryDateTo.Value.Date;

                query =
                    query.Where(x =>
                        x.DeliveryDate.HasValue &&
                        x.DeliveryDate.Value.Date <= date);
            }

            if (!string.IsNullOrWhiteSpace(columnAssignedDashboard))
            {
                var value =
                    columnAssignedDashboard.Trim();

                query =
                    query.Where(x =>
                        x.AssignedDashboard == value);
            }

            return query;
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

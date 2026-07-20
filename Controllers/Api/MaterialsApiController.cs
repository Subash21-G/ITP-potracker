using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;
using System.Linq.Expressions;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/materials")]
    [Produces("application/json")]
    public sealed class MaterialsApiController : ControllerBase
    {
        private const int DefaultPageSize = 25;
        private const int MaximumPageSize = 100;

        private readonly ApplicationDbContext _context;

        private static readonly Expression<
            Func<Material, MaterialListItemDto>> MaterialProjection =
                material => new MaterialListItemDto
                {
                    Id = material.Id,
                    PurchaseOrderId = material.PurchaseOrderId,
                    PONumber = material.PurchaseOrder != null
                        ? material.PurchaseOrder.PONumber
                        : string.Empty,
                    Handler = material.PurchaseOrder != null
                        ? material.PurchaseOrder.BuyerContactPerson ??
                          string.Empty
                        : string.Empty,
                    PODate = material.PurchaseOrder != null
                        ? material.PurchaseOrder.PODate ?? material.PODate
                        : material.PODate,
                    POReceivedDate = material.PurchaseOrder != null
                        ? material.PurchaseOrder.ReceivedDate ??
                          material.ReceivingDate
                        : material.ReceivingDate,
                    LineItem = material.LineItem ?? string.Empty,
                    MaterialNo = material.MaterialNo ?? string.Empty,
                    HsnSacCode = material.HsnSacCode,
                    Description = material.ShortDescription != null &&
                                  material.ShortDescription != string.Empty
                        ? material.ShortDescription
                        : material.BriefDescription ?? string.Empty,
                    Quantity = material.Qty,
                    Unit = material.Unit ?? string.Empty,
                    InvoiceQuantity = material.InvoiceQty,
                    DispatchQuantity = material.DispatchQty,
                    PendingQuantity = material.PendingQty,
                    RecordedStockQuantity = material.StockQty,
                    InvoiceStatus = material.InvoiceStatus,
                    ProcessStatus = material.ProcessStatus,
                    DispatchStatus = material.DispatchStatus,
                    DeliveryDate = material.DeliveryDate,
                    DrawingNo = material.DrawingNo ?? string.Empty,
                    AssignedDashboard =
                        material.AssignedDashboard ?? string.Empty
                };

        public MaterialsApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns one SQL-paginated page of materials for Angular.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(
            typeof(PagedResult<MaterialListItemDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PagedResult<MaterialListItemDto>>>
            Index(
                string? search,
                string? poNumber,
                string? handler,
                string? invoiceStatus,
                string? dispatchStatus,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                int page = 1,
                int pageSize = DefaultPageSize,
                CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);
            pageSize = NormalizePageSize(pageSize);

            var query = ApplyFilters(
                _context.Materials.AsNoTracking(),
                search,
                poNumber,
                handler,
                invoiceStatus,
                dispatchStatus,
                deliveryDateFrom,
                deliveryDateTo);

            var totalRecords = await query.CountAsync(
                cancellationToken);

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalRecords / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var items = await query
                .OrderByDescending(material => material.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MaterialProjection)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResult<MaterialListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            });
        }

        /// <summary>
        /// Returns a single material without serializing EF navigation cycles.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(
            typeof(MaterialListItemDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MaterialListItemDto>> Details(
            int id,
            CancellationToken cancellationToken = default)
        {
            var material = await _context.Materials
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(MaterialProjection)
                .FirstOrDefaultAsync(cancellationToken);

            if (material == null)
            {
                return NotFound(new
                {
                    error = $"Material {id} was not found."
                });
            }

            return Ok(material);
        }

        private static IQueryable<Material> ApplyFilters(
            IQueryable<Material> query,
            string? search,
            string? poNumber,
            string? handler,
            string? invoiceStatus,
            string? dispatchStatus,
            DateTime? deliveryDateFrom,
            DateTime? deliveryDateTo)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();

                query = query.Where(material =>
                    (material.MaterialNo != null &&
                     material.MaterialNo.Contains(value)) ||
                    (material.LineItem != null &&
                     material.LineItem.Contains(value)) ||
                    (material.HsnSacCode != null &&
                     material.HsnSacCode.Contains(value)) ||
                    (material.ShortDescription != null &&
                     material.ShortDescription.Contains(value)) ||
                    (material.BriefDescription != null &&
                     material.BriefDescription.Contains(value)) ||
                    (material.DrawingNo != null &&
                     material.DrawingNo.Contains(value)) ||
                    (material.PurchaseOrder != null &&
                     material.PurchaseOrder.PONumber.Contains(value)));
            }

            if (!string.IsNullOrWhiteSpace(poNumber))
            {
                var value = poNumber.Trim();

                query = query.Where(material =>
                    material.PurchaseOrder != null &&
                    material.PurchaseOrder.PONumber.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(handler))
            {
                var value = handler.Trim();

                query = query.Where(material =>
                    material.PurchaseOrder != null &&
                    material.PurchaseOrder.BuyerContactPerson != null &&
                    material.PurchaseOrder.BuyerContactPerson
                        .Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(invoiceStatus))
            {
                query = query.Where(material =>
                    material.InvoiceStatus == invoiceStatus);
            }

            if (!string.IsNullOrWhiteSpace(dispatchStatus))
            {
                query = query.Where(material =>
                    material.DispatchStatus == dispatchStatus);
            }

            if (deliveryDateFrom.HasValue)
            {
                var startDate = deliveryDateFrom.Value.Date;

                query = query.Where(material =>
                    material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value >= startDate);
            }

            if (deliveryDateTo.HasValue)
            {
                var endDateExclusive =
                    deliveryDateTo.Value.Date.AddDays(1);

                query = query.Where(material =>
                    material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value < endDateExclusive);
            }

            return query;
        }

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize < 1)
            {
                return DefaultPageSize;
            }

            return Math.Min(pageSize, MaximumPageSize);
        }
    }
}

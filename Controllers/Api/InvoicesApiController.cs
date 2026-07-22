using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using System.Text.RegularExpressions;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/invoices")]
    public sealed class InvoicesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PendingInvoiceMatcher _pendingInvoiceMatcher;

        public InvoicesApiController(
            ApplicationDbContext context,
            PendingInvoiceMatcher pendingInvoiceMatcher)
        {
            _context = context;
            _pendingInvoiceMatcher = pendingInvoiceMatcher;
        }

        // ============================================================
        // INVOICE LIST
        // ============================================================

        [HttpGet]
        public async Task<ActionResult<InvoiceListResponseDto>> Index(
            string? search,
            DateTime? dateFrom,
            DateTime? dateTo,
            int page = 1,
            int pageSize = 25)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 200);

            var query = _context.Invoices
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchValue = search.Trim();

                query = query.Where(invoice =>
                    invoice.InvoiceNumber.Contains(searchValue) ||
                    (invoice.CustomerName != null &&
                     invoice.CustomerName.Contains(searchValue)) ||
                    (invoice.UploadedFileName != null &&
                     invoice.UploadedFileName.Contains(searchValue)));
            }

            if (dateFrom.HasValue)
            {
                var fromDate = dateFrom.Value.Date;

                query = query.Where(invoice =>
                    invoice.InvoiceDate.HasValue &&
                    invoice.InvoiceDate.Value >= fromDate);
            }

            if (dateTo.HasValue)
            {
                var nextDate = dateTo.Value.Date.AddDays(1);

                query = query.Where(invoice =>
                    invoice.InvoiceDate.HasValue &&
                    invoice.InvoiceDate.Value < nextDate);
            }

            var totalCount = await query.CountAsync();

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalCount / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var items = await query
                .OrderByDescending(invoice => invoice.InvoiceDate)
                .ThenByDescending(invoice => invoice.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(invoice => new InvoiceListItemDto
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    CustomerName = invoice.CustomerName ?? string.Empty,
                    UploadedFileName =
                        invoice.UploadedFileName ?? string.Empty,
                    CreatedDate = invoice.CreatedDate,
                    ItemCount = invoice.Items.Count,
                    TotalQuantity = invoice.Items
                        .Sum(item => (decimal?)item.InvoiceQty) ?? 0m,
                    TotalGstAmount = invoice.Items
                        .Sum(item => (decimal?)item.GstAmount) ?? 0m,
                    TotalAmount = invoice.Items
                        .Sum(item => (decimal?)item.Amount) ?? 0m
                })
                .ToListAsync();

            var summary = new InvoiceListSummaryDto
            {
                TotalInvoices = await _context.Invoices
                    .AsNoTracking()
                    .CountAsync(),

                TotalItems = await _context.InvoiceItems
                    .AsNoTracking()
                    .CountAsync(),

                TotalQuantity = await _context.InvoiceItems
                    .AsNoTracking()
                    .SumAsync(item =>
                        (decimal?)item.InvoiceQty) ?? 0m,

                TotalGstAmount = await _context.InvoiceItems
                    .AsNoTracking()
                    .SumAsync(item =>
                        (decimal?)item.GstAmount) ?? 0m,

                TotalInvoiceAmount = await _context.InvoiceItems
                    .AsNoTracking()
                    .SumAsync(item =>
                        (decimal?)item.Amount) ?? 0m,

                PendingItemCount = await _context.PendingInvoiceItems
                    .AsNoTracking()
                    .CountAsync(item =>
                        item.Status == "Pending")
            };

            return Ok(new InvoiceListResponseDto
            {
                Items = items,
                Summary = summary,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        // ============================================================
        // INVOICE DETAILS
        // ============================================================

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InvoiceDetailsApiDto>> Details(
            int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Invalid invoice ID."
                });
            }

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.Id,
                    item.InvoiceNumber,
                    item.InvoiceDate,
                    item.CustomerName,
                    item.UploadedFileName,
                    item.UploadedFilePath,
                    item.CreatedDate
                })
                .FirstOrDefaultAsync();

            if (invoice == null)
            {
                return NotFound(new
                {
                    message = "Invoice was not found."
                });
            }

            var invoiceItems = await _context.InvoiceItems
                .AsNoTracking()
                .Where(item => item.InvoiceId == id)
                .OrderBy(item => item.Id)
                .Select(item => new InvoiceItemApiDto
                {
                    Id = item.Id,
                    PONumber = item.PONumber ?? string.Empty,
                    LineItem = item.LineItem ?? string.Empty,
                    MaterialNo = item.MaterialNo ?? string.Empty,
                    Qty = item.InvoiceQty,
                    PriceUnit = item.PriceUnit,
                    GstAmount = item.GstAmount,
                    Amount = item.Amount,
                    PODate = null
                })
                .ToListAsync();

            return Ok(new InvoiceDetailsApiDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,
                CustomerName = invoice.CustomerName ?? string.Empty,
                UploadedFileName =
                    invoice.UploadedFileName ?? string.Empty,
                UploadedFilePath =
                    invoice.UploadedFilePath ?? string.Empty,
                CreatedDate = invoice.CreatedDate,
                TotalQuantity = invoiceItems.Sum(item => item.Qty),
                TotalGstAmount = invoiceItems.Sum(item => item.GstAmount),
                TotalAmount = invoiceItems.Sum(item => item.Amount),
                Items = invoiceItems
            });
        }

        // ============================================================
        // IMPORT HISTORY
        // ============================================================

        [HttpGet("import-history")]
        public async Task<ActionResult<List<InvoiceImportBatchDto>>>
            ImportHistory(
                string? search,
                int take = 200)
        {
            take = Math.Clamp(take, 10, 500);

            var query = _context.VyaparInvoiceImportBatches
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchValue = search.Trim();

                query = query.Where(batch =>
                    batch.FileName != null &&
                    batch.FileName.Contains(searchValue));
            }

            var batches = await query
                .OrderByDescending(batch => batch.ImportDate)
                .Take(take)
                .Select(batch => new InvoiceImportBatchDto
                {
                    Id = batch.Id,
                    FileName = batch.FileName ?? string.Empty,
                    ImportDate = batch.ImportDate,
                    TotalRows = batch.TotalRows,
                    SuccessRows = batch.SuccessRows,
                    FailedRows = batch.FailedRows,
                    PendingRows = batch.Rows.Count(row =>
                        row.Status == "Pending")
                })
                .ToListAsync();

            return Ok(batches);
        }

        // ============================================================
        // PENDING / UNMATCHED INVOICE ITEMS
        // ============================================================

        [HttpGet("pending")]
        public async Task<ActionResult<List<PendingInvoiceItemApiDto>>>
            Pending(
                string? search,
                int take = 500)
        {
            take = Math.Clamp(take, 10, 1000);

            var query = _context.PendingInvoiceItems
                .AsNoTracking()
                .Where(item => item.Status == "Pending");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchValue = search.Trim();

                query = query.Where(item =>
                    item.InvoiceNumber.Contains(searchValue) ||
                    (item.PONumber != null &&
                     item.PONumber.Contains(searchValue)) ||
                    (item.MaterialNo != null &&
                     item.MaterialNo.Contains(searchValue)) ||
                    (item.LineItem != null &&
                     item.LineItem.Contains(searchValue)));
            }

            var items = await query
                .OrderByDescending(item => item.CreatedDate)
                .Take(take)
                .Select(item => new PendingInvoiceItemApiDto
                {
                    Id = item.Id,
                    InvoiceNumber = item.InvoiceNumber,
                    InvoiceDate = item.InvoiceDate,
                    PONumber = item.PONumber ?? string.Empty,
                    LineItem = item.LineItem ?? string.Empty,
                    MaterialNo = item.MaterialNo ?? string.Empty,
                    Qty = item.InvoiceQty,
                    PriceUnit = item.PriceUnit,
                    GstAmount = item.GstAmount,
                    Amount = item.Amount,
                    ErrorMessage = item.ErrorMessage ?? string.Empty,
                    CreatedDate = item.CreatedDate
                })
                .ToListAsync();

            return Ok(items);
        }


        // ============================================================
        // MANUAL MATCH CANDIDATES
        // Admin and Production use this endpoint from the Angular
        // pending-row review screen.
        // ============================================================

        [HttpGet("pending/{id:int}/candidates")]
        public async Task<ActionResult<
            PendingInvoiceMatchCandidatesResponseDto>>
            PendingCandidates(
                int id,
                string? search,
                int take = 50)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Invalid pending invoice row ID."
                });
            }

            take = Math.Clamp(take, 10, 200);

            var pending = await _context.PendingInvoiceItems
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.Status == "Pending");

            if (pending == null)
            {
                return NotFound(new
                {
                    message =
                        "The pending invoice row was not found or is already matched."
                });
            }

            var pendingPoKey = NormalizeKey(
                pending.PONumber);

            var pendingMaterialKey = NormalizeKey(
                pending.MaterialNo);

            var pendingLineKey = NormalizeLineItem(
                pending.LineItem);

            var searchValue = search?.Trim() ?? string.Empty;
            var searchKey = NormalizeKey(searchValue);
            var searchLineKey = NormalizeLineItem(searchValue);

            var materials = await _context.Materials
                .AsNoTracking()
                .ToListAsync();

            var purchaseOrderIds = materials
                .Select(material => material.PurchaseOrderId)
                .Distinct()
                .ToList();

            var purchaseOrderNumberMap =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .Where(purchaseOrder =>
                        purchaseOrderIds.Contains(
                            purchaseOrder.Id))
                    .ToDictionaryAsync(
                        purchaseOrder => purchaseOrder.Id,
                        purchaseOrder =>
                            purchaseOrder.PONumber ?? string.Empty);

            var candidates = materials
                .Select(material =>
                {
                    var poNumber =
                        purchaseOrderNumberMap.TryGetValue(
                            material.PurchaseOrderId,
                            out var storedPONumber)
                            ? storedPONumber.Trim()
                            : string.Empty;

                    var materialNo =
                        material.MaterialNo?.Trim() ??
                        string.Empty;

                    var lineItem =
                        material.LineItem?.Trim() ??
                        string.Empty;

                    var poKey = NormalizeKey(poNumber);
                    var materialKey = NormalizeKey(materialNo);
                    var lineKey = NormalizeLineItem(lineItem);

                    var exactPo =
                        !string.IsNullOrWhiteSpace(pendingPoKey) &&
                        poKey == pendingPoKey;

                    var exactMaterial =
                        !string.IsNullOrWhiteSpace(pendingMaterialKey) &&
                        materialKey == pendingMaterialKey;

                    var exactLine =
                        !string.IsNullOrWhiteSpace(pendingLineKey) &&
                        lineKey == pendingLineKey;

                    var balanceQty =
                        GetAvailableInvoiceBalance(material);

                    var score = 0;

                    if (exactPo)
                    {
                        score += 100;
                    }

                    if (exactMaterial)
                    {
                        score += 80;
                    }

                    if (exactLine)
                    {
                        score += 40;
                    }

                    if (!exactMaterial &&
                        !string.IsNullOrWhiteSpace(pendingMaterialKey) &&
                        (materialKey.Contains(pendingMaterialKey) ||
                         pendingMaterialKey.Contains(materialKey)))
                    {
                        score += 25;
                    }

                    if (!exactPo &&
                        !string.IsNullOrWhiteSpace(pendingPoKey) &&
                        (poKey.Contains(pendingPoKey) ||
                         pendingPoKey.Contains(poKey)))
                    {
                        score += 20;
                    }

                    if (!exactLine &&
                        !string.IsNullOrWhiteSpace(pendingLineKey) &&
                        lineKey == pendingLineKey)
                    {
                        score += 10;
                    }

                    var searchMatches =
                        string.IsNullOrWhiteSpace(searchValue) ||
                        (!string.IsNullOrWhiteSpace(searchKey) &&
                         (poKey.Contains(searchKey) ||
                          materialKey.Contains(searchKey) ||
                          NormalizeKey(material.ShortDescription)
                              .Contains(searchKey))) ||
                        (!string.IsNullOrWhiteSpace(searchLineKey) &&
                         lineKey == searchLineKey);

                    return new
                    {
                        Material = material,
                        PONumber = poNumber,
                        LineItem = lineItem,
                        MaterialNo = materialNo,
                        BalanceQty = balanceQty,
                        Score = score,
                        ExactPo = exactPo,
                        ExactMaterial = exactMaterial,
                        ExactLine = exactLine,
                        SearchMatches = searchMatches
                    };
                })
                .Where(candidate =>
                    candidate.SearchMatches)
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate =>
                    candidate.BalanceQty >= pending.InvoiceQty)
                .ThenByDescending(candidate => candidate.ExactMaterial)
                .ThenByDescending(candidate => candidate.ExactPo)
                .ThenBy(candidate => candidate.PONumber)
                .ThenBy(candidate => candidate.LineItem)
                .Take(take)
                .Select(candidate =>
                    new PendingInvoiceMatchCandidateDto
                    {
                        MaterialId = candidate.Material.Id,
                        PurchaseOrderId =
                            candidate.Material.PurchaseOrderId,
                        PONumber = candidate.PONumber,
                        LineItem = candidate.LineItem,
                        MaterialNo = candidate.MaterialNo,
                        ShortDescription =
                            candidate.Material.ShortDescription ??
                            string.Empty,
                        POQty = candidate.Material.Qty,
                        InvoiceQty =
                            candidate.Material.InvoiceQty,
                        BalanceQty = candidate.BalanceQty,
                        DispatchQty =
                            candidate.Material.DispatchQty,
                        PendingQty =
                            candidate.Material.PendingQty,
                        MatchScore = candidate.Score,
                        IsExactPO = candidate.ExactPo,
                        IsExactMaterial =
                            candidate.ExactMaterial,
                        IsExactLineItem =
                            candidate.ExactLine,
                        HasEnoughBalance =
                            pending.InvoiceQty > 0 &&
                            pending.InvoiceQty <=
                            candidate.BalanceQty
                    })
                .ToList();

            return Ok(
                new PendingInvoiceMatchCandidatesResponseDto
                {
                    PendingItem =
                        MapPendingInvoiceItem(pending),
                    Candidates = candidates
                });
        }

        // ============================================================
        // MANUAL MATCH ONE PENDING ROW
        // ============================================================

        [HttpPost("pending/{id:int}/match")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<
            PendingInvoiceManualMatchResponseDto>>
            MatchPendingManually(
                int id,
                [FromBody]
                PendingInvoiceManualMatchRequestDto request)
        {
            if (id <= 0 || request.MaterialId <= 0)
            {
                return BadRequest(
                    new PendingInvoiceManualMatchResponseDto
                    {
                        Success = false,
                        Message =
                            "A valid pending row and material are required.",
                        PendingItemId = id,
                        MaterialId = request.MaterialId
                    });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var pending = await _context.PendingInvoiceItems
                    .FirstOrDefaultAsync(item =>
                        item.Id == id &&
                        item.Status == "Pending");

                if (pending == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "The pending row was not found or is already matched.",
                            PendingItemId = id,
                            MaterialId = request.MaterialId
                        });
                }

                var invoiceExists = await _context.Invoices
                    .AnyAsync(invoice =>
                        invoice.Id == pending.InvoiceId);

                if (!invoiceExists)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "The invoice linked to this pending row was not found.",
                            PendingItemId = pending.Id,
                            MaterialId = request.MaterialId,
                            InvoiceId = pending.InvoiceId
                        });
                }

                var material = await _context.Materials
                    .Include(item => item.PurchaseOrder)
                    .FirstOrDefaultAsync(item =>
                        item.Id == request.MaterialId);

                if (material == null ||
                    material.PurchaseOrder == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "The selected PO material was not found.",
                            PendingItemId = pending.Id,
                            MaterialId = request.MaterialId,
                            InvoiceId = pending.InvoiceId
                        });
                }

                if (pending.InvoiceQty <= 0)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "Pending invoice quantity must be greater than zero.",
                            PendingItemId = pending.Id,
                            MaterialId = material.Id,
                            InvoiceId = pending.InvoiceId
                        });
                }

                var actualPoNumber =
                    material.PurchaseOrder.PONumber?.Trim() ??
                    string.Empty;

                var actualMaterialNo =
                    material.MaterialNo?.Trim() ??
                    string.Empty;

                var actualLineItem =
                    material.LineItem?.Trim() ??
                    string.Empty;

                var exactPo =
                    NormalizeKey(pending.PONumber) ==
                    NormalizeKey(actualPoNumber);

                var exactMaterial =
                    NormalizeKey(pending.MaterialNo) ==
                    NormalizeKey(actualMaterialNo);

                var pendingLineKey =
                    NormalizeLineItem(pending.LineItem);

                var materialLineKey =
                    NormalizeLineItem(actualLineItem);

                var exactLine =
                    string.IsNullOrWhiteSpace(pendingLineKey) ||
                    string.IsNullOrWhiteSpace(materialLineKey) ||
                    pendingLineKey == materialLineKey;

                if ((!exactPo ||
                     !exactMaterial ||
                     !exactLine) &&
                    !request.ConfirmMismatch)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "The selected material does not exactly match the imported PO/material/line item. Confirm the mismatch before saving.",
                            PendingItemId = pending.Id,
                            MaterialId = material.Id,
                            InvoiceId = pending.InvoiceId
                        });
                }

                var resolvedLineItem =
                    string.IsNullOrWhiteSpace(actualLineItem)
                        ? pending.LineItem ?? string.Empty
                        : actualLineItem;

                var alreadySaved =
                    await _context.InvoiceItems
                        .AnyAsync(item =>
                            item.InvoiceId ==
                                pending.InvoiceId &&
                            item.MaterialId ==
                                material.Id &&
                            item.LineItem ==
                                resolvedLineItem &&
                            item.InvoiceQty ==
                                pending.InvoiceQty);

                if (!alreadySaved)
                {
                    var balanceQty =
                        GetAvailableInvoiceBalance(material);

                    if (pending.InvoiceQty > balanceQty)
                    {
                        await transaction.RollbackAsync();

                        return BadRequest(
                            new PendingInvoiceManualMatchResponseDto
                            {
                                Success = false,
                                Message =
                                    $"Invoice qty {pending.InvoiceQty} is greater than the selected material balance qty {balanceQty}.",
                                PendingItemId = pending.Id,
                                MaterialId = material.Id,
                                InvoiceId = pending.InvoiceId
                            });
                    }

                    _context.InvoiceItems.Add(
                        new InvoiceItem
                        {
                            InvoiceId =
                                pending.InvoiceId,
                            MaterialId =
                                material.Id,
                            PONumber =
                                actualPoNumber,
                            LineItem =
                                resolvedLineItem,
                            MaterialNo =
                                actualMaterialNo,
                            Description =
                                pending.Description,
                            HsnSacCode =
                                pending.HsnSacCode,
                            InvoiceQty =
                                pending.InvoiceQty,
                            PriceUnit =
                                pending.PriceUnit,
                            GstAmount =
                                pending.GstAmount,
                            Amount =
                                pending.Amount,
                            CreatedDate =
                                DateTime.Now
                        });

                    material.InvoiceQty +=
                        pending.InvoiceQty;

                    material.BalanceQty =
                        Math.Max(
                            0m,
                            material.Qty -
                            material.InvoiceQty);

                    material.InvoiceStatus =
                        GetInvoiceStatus(
                            material.Qty,
                            material.InvoiceQty,
                            material.BalanceQty);

                    material.DispatchQty +=
                        pending.InvoiceQty;

                    material.PendingQty =
                        Math.Max(
                            0m,
                            material.Qty -
                            material.DispatchQty);

                    material.DispatchDate =
                        pending.InvoiceDate ??
                        DateTime.Today;

                    material.DispatchStatus =
                        GetDispatchStatus(
                            material.Qty,
                            material.DispatchQty,
                            material.PendingQty);
                }

                pending.Status = "Matched";
                pending.MatchedMaterialId = material.Id;
                pending.MatchedDate = DateTime.Now;
                pending.ErrorMessage = null;

                await MarkImportAuditAsManualSuccessAsync(
                    pending,
                    actualPoNumber,
                    actualMaterialNo,
                    resolvedLineItem);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(
                    new PendingInvoiceManualMatchResponseDto
                    {
                        Success = true,
                        Message = alreadySaved
                            ? "The invoice item already existed. The pending row was safely marked as matched without changing quantities again."
                            : $"Pending row matched to PO {actualPoNumber}, material {actualMaterialNo}, line {resolvedLineItem}.",
                        PendingItemId = pending.Id,
                        MaterialId = material.Id,
                        InvoiceId = pending.InvoiceId
                    });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new PendingInvoiceManualMatchResponseDto
                    {
                        Success = false,
                        Message =
                            "Manual matching failed. No partial changes were saved.",
                        PendingItemId = id,
                        MaterialId = request.MaterialId
                    });
            }
        }

        // ============================================================
        // RETRY ALL PENDING INVOICE MATCHES
        // Admin and Production only. Viewer POST requests are blocked
        // by SessionAuthorizationFilter.
        // ============================================================

        [HttpPost("pending/retry")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<PendingInvoiceRetryResponseDto>>
            RetryPending()
        {
            var pendingBefore = await _context.PendingInvoiceItems
                .CountAsync(item => item.Status == "Pending");

            if (pendingBefore == 0)
            {
                return Ok(new PendingInvoiceRetryResponseDto
                {
                    Success = true,
                    Message = "There are no pending invoice rows to retry.",
                    PendingBefore = 0,
                    MatchedRows = 0,
                    RemainingRows = 0
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var purchaseOrderIds = await _context.Materials
                    .AsNoTracking()
                    .Select(material => material.PurchaseOrderId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToListAsync();

                foreach (var purchaseOrderId in purchaseOrderIds)
                {
                    await _pendingInvoiceMatcher
                        .MatchPendingInvoicesForPurchaseOrderAsync(
                            purchaseOrderId);
                }

                var remainingRows = await _context.PendingInvoiceItems
                    .CountAsync(item => item.Status == "Pending");

                var matchedRows = Math.Max(
                    0,
                    pendingBefore - remainingRows);

                var batches = await _context
                    .VyaparInvoiceImportBatches
                    .Include(batch => batch.Rows)
                    .ToListAsync();

                foreach (var batch in batches)
                {
                    batch.SuccessRows = batch.Rows.Count(row =>
                        row.Status == "Success");

                    batch.FailedRows = batch.Rows.Count(row =>
                        row.Status == "Failed");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var message = matchedRows > 0
                    ? $"{matchedRows} pending invoice row(s) were " +
                      $"matched successfully. {remainingRows} row(s) " +
                      "still require PO/material corrections."
                    : "No pending rows could be matched. Check the PO " +
                      "number, material number, line item and available " +
                      "balance quantity.";

                return Ok(new PendingInvoiceRetryResponseDto
                {
                    Success = true,
                    Message = message,
                    PendingBefore = pendingBefore,
                    MatchedRows = matchedRows,
                    RemainingRows = remainingRows
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new PendingInvoiceRetryResponseDto
                    {
                        Success = false,
                        Message =
                            "Retry matching failed. No partial matching " +
                            "changes were committed.",
                        PendingBefore = pendingBefore,
                        MatchedRows = 0,
                        RemainingRows = pendingBefore
                    });
            }
        }

        // ============================================================
        // DELETE INVOICE AND RESTORE MATERIAL QUANTITIES
        // ============================================================

        [HttpDelete("{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<InvoiceDeleteResponseDto>> Delete(
            int id)
        {
            if (id <= 0)
            {
                return BadRequest(new InvoiceDeleteResponseDto
                {
                    Success = false,
                    Message = "Invalid invoice ID.",
                    InvoiceId = id
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var invoice = await _context.Invoices
                    .Include(item => item.Items)
                    .FirstOrDefaultAsync(item => item.Id == id);

                if (invoice == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(new InvoiceDeleteResponseDto
                    {
                        Success = false,
                        Message = "Invoice was not found.",
                        InvoiceId = id
                    });
                }

                var quantityByMaterial = invoice.Items
                    .GroupBy(item => item.MaterialId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(item => item.InvoiceQty));

                var materialIds = quantityByMaterial.Keys.ToList();

                var materials = await _context.Materials
                    .Where(material => materialIds.Contains(material.Id))
                    .ToListAsync();

                foreach (var material in materials)
                {
                    if (!quantityByMaterial.TryGetValue(
                            material.Id,
                            out var invoiceQuantity))
                    {
                        continue;
                    }

                    material.InvoiceQty = Math.Max(
                        0m,
                        material.InvoiceQty - invoiceQuantity);

                    material.BalanceQty = Math.Max(
                        0m,
                        material.Qty - material.InvoiceQty);

                    material.InvoiceStatus = GetInvoiceStatus(
                        material.Qty,
                        material.InvoiceQty,
                        material.BalanceQty);

                    material.DispatchQty = Math.Max(
                        0m,
                        material.DispatchQty - invoiceQuantity);

                    material.PendingQty = Math.Max(
                        0m,
                        material.Qty - material.DispatchQty);

                    if (material.DispatchQty <= 0)
                    {
                        material.DispatchDate = null;
                    }

                    material.DispatchStatus = GetDispatchStatus(
                        material.Qty,
                        material.DispatchQty,
                        material.PendingQty);
                }

                var pendingItems = await _context.PendingInvoiceItems
                    .Where(item => item.InvoiceId == invoice.Id)
                    .ToListAsync();

                if (pendingItems.Count > 0)
                {
                    _context.PendingInvoiceItems.RemoveRange(
                        pendingItems);
                }

                var importRows = await _context.VyaparInvoiceImportRows
                    .Where(row =>
                        row.InvoiceNumber == invoice.InvoiceNumber)
                    .ToListAsync();

                foreach (var row in importRows)
                {
                    row.Status = "Deleted";
                    row.ErrorMessage =
                        "Invoice deleted. This row can be imported again.";
                }

                _context.Invoices.Remove(invoice);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new InvoiceDeleteResponseDto
                {
                    Success = true,
                    Message =
                        $"Invoice {invoice.InvoiceNumber} was deleted " +
                        "and material invoice/dispatch quantities were restored.",
                    InvoiceId = invoice.Id
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new InvoiceDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Unable to delete the invoice. " +
                            "No partial changes were saved.",
                        InvoiceId = id
                    });
            }
        }

        // ============================================================
        // DELETE IMPORT HISTORY ONLY
        // Actual invoices and material quantities are not changed.
        // ============================================================

        [HttpDelete("import-history/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<
            InvoiceImportBatchDeleteResponseDto>> DeleteImportBatch(
                int id)
        {
            if (id <= 0)
            {
                return BadRequest(
                    new InvoiceImportBatchDeleteResponseDto
                    {
                        Success = false,
                        Message = "Invalid import batch ID.",
                        BatchId = id
                    });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var batch = await _context.VyaparInvoiceImportBatches
                    .FirstOrDefaultAsync(item => item.Id == id);

                if (batch == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        new InvoiceImportBatchDeleteResponseDto
                        {
                            Success = false,
                            Message = "Import history was not found.",
                            BatchId = id
                        });
                }

                var rows = await _context.VyaparInvoiceImportRows
                    .Where(row => row.BatchId == id)
                    .ToListAsync();

                if (rows.Count > 0)
                {
                    _context.VyaparInvoiceImportRows.RemoveRange(rows);
                }

                _context.VyaparInvoiceImportBatches.Remove(batch);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new InvoiceImportBatchDeleteResponseDto
                {
                    Success = true,
                    Message =
                        $"Import history '{batch.FileName}' was deleted. " +
                        "Actual invoices and material quantities were not changed.",
                    BatchId = batch.Id
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new InvoiceImportBatchDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Unable to delete import history. " +
                            "No partial changes were saved.",
                        BatchId = id
                    });
            }
        }


        private static PendingInvoiceItemApiDto
            MapPendingInvoiceItem(
                PendingInvoiceItem item)
        {
            return new PendingInvoiceItemApiDto
            {
                Id = item.Id,
                InvoiceNumber =
                    item.InvoiceNumber ?? string.Empty,
                InvoiceDate = item.InvoiceDate,
                PONumber =
                    item.PONumber ?? string.Empty,
                LineItem =
                    item.LineItem ?? string.Empty,
                MaterialNo =
                    item.MaterialNo ?? string.Empty,
                Qty = item.InvoiceQty,
                PriceUnit = item.PriceUnit,
                GstAmount = item.GstAmount,
                Amount = item.Amount,
                ErrorMessage =
                    item.ErrorMessage ?? string.Empty,
                CreatedDate = item.CreatedDate
            };
        }

        private static decimal GetAvailableInvoiceBalance(
            Material material)
        {
            var balanceQty =
                material.BalanceQty > 0
                    ? material.BalanceQty
                    : material.Qty -
                      material.InvoiceQty;

            if (material.InvoiceQty == 0 &&
                material.BalanceQty == 0)
            {
                balanceQty = material.Qty;
            }

            return Math.Max(0m, balanceQty);
        }

        private async Task MarkImportAuditAsManualSuccessAsync(
            PendingInvoiceItem pending,
            string actualPoNumber,
            string actualMaterialNo,
            string actualLineItem)
        {
            var invoiceNumber =
                pending.InvoiceNumber?.Trim() ??
                string.Empty;

            var poKey =
                NormalizeKey(pending.PONumber);

            var materialKey =
                NormalizeKey(pending.MaterialNo);

            var lineKey =
                NormalizeLineItem(pending.LineItem);

            var rows = await _context
                .VyaparInvoiceImportRows
                .Where(row =>
                    row.Status == "Pending" &&
                    row.InvoiceNumber == invoiceNumber)
                .ToListAsync();

            foreach (var row in rows.Where(row =>
                NormalizeKey(row.PONumber) == poKey &&
                NormalizeKey(row.MaterialNumber) == materialKey &&
                NormalizeLineItem(row.LineItem) == lineKey &&
                row.InvoiceQty == pending.InvoiceQty))
            {
                row.Status = "Success";
                row.ErrorMessage =
                    $"Manually matched to PO {actualPoNumber}, material {actualMaterialNo}, line {actualLineItem}.";
            }
        }

        private static string NormalizeKey(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(
                    value.Trim(),
                    @"[^A-Za-z0-9]",
                    string.Empty)
                .ToUpperInvariant();
        }

        private static string NormalizeLineItem(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var digits = Regex.Replace(
                value.Trim(),
                @"[^0-9]",
                string.Empty);

            return string.IsNullOrWhiteSpace(digits)
                ? string.Empty
                : "N-" + digits;
        }

        // ============================================================
        // STATUS HELPERS
        // ============================================================

        private static string GetInvoiceStatus(
            decimal poQty,
            decimal invoiceQty,
            decimal balanceQty)
        {
            if (invoiceQty <= 0)
            {
                return "Not Invoiced";
            }

            if (balanceQty <= 0 || invoiceQty >= poQty)
            {
                return "Fully Invoiced";
            }

            return "Partially Invoiced";
        }

        private static string GetDispatchStatus(
            decimal poQty,
            decimal dispatchQty,
            decimal pendingQty)
        {
            if (dispatchQty <= 0)
            {
                return "Pending";
            }

            if (pendingQty <= 0 || dispatchQty >= poQty)
            {
                return "Dispatched";
            }

            return "Partial";
        }
    }
}
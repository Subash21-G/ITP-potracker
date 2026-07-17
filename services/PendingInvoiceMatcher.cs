using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using System.Text.RegularExpressions;

namespace POTracker.Services
{
    public class PendingInvoiceMatcher
    {
        private readonly ApplicationDbContext _context;

        public PendingInvoiceMatcher(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // MATCH ALL PENDING INVOICE ITEMS FOR ONE PURCHASE ORDER
        // ============================================================

        public async Task<int>
            MatchPendingInvoicesForPurchaseOrderAsync(
                int purchaseOrderId)
        {
            var materials =
                await _context.Materials
                    .Include(x => x.PurchaseOrder)
                    .Where(x =>
                        x.PurchaseOrderId ==
                        purchaseOrderId)
                    .OrderBy(x => x.Id)
                    .ToListAsync();

            if (materials.Count == 0)
            {
                return 0;
            }

            var totalMatched = 0;

            foreach (var material in materials)
            {
                totalMatched +=
                    await MatchPendingInvoicesForMaterialInternalAsync(
                        material);
            }

            await _context.SaveChangesAsync();

            return totalMatched;
        }

        // ============================================================
        // MATCH PENDING INVOICE ITEMS FOR ONE MATERIAL
        // ============================================================

        public async Task<int>
            MatchPendingInvoicesForMaterialAsync(
                int materialId)
        {
            var material =
                await _context.Materials
                    .Include(x => x.PurchaseOrder)
                    .FirstOrDefaultAsync(x =>
                        x.Id == materialId);

            if (material == null)
            {
                return 0;
            }

            var matchedCount =
                await MatchPendingInvoicesForMaterialInternalAsync(
                    material);

            await _context.SaveChangesAsync();

            return matchedCount;
        }

        // ============================================================
        // INTERNAL MATCHING LOGIC
        // ============================================================

        private async Task<int>
            MatchPendingInvoicesForMaterialInternalAsync(
                Material material)
        {
            if (material.PurchaseOrder == null)
            {
                return 0;
            }

            var poNumber =
                material.PurchaseOrder.PONumber
                    ?.Trim() ?? "";

            var materialNo =
                material.MaterialNo
                    ?.Trim() ?? "";

            var materialLineItem =
                NormalizeLineItem(
                    material.LineItem);

            if (string.IsNullOrWhiteSpace(poNumber) ||
                string.IsNullOrWhiteSpace(materialNo))
            {
                return 0;
            }

            var poKey =
                NormalizeKey(poNumber);

            var materialKey =
                NormalizeKey(materialNo);

            // --------------------------------------------------------
            // FIND ALL MATERIAL CANDIDATES WITH SAME PO + MATERIAL
            // --------------------------------------------------------

            var samePoMaterials =
                await _context.Materials
                    .Include(x => x.PurchaseOrder)
                    .Where(x =>
                        x.PurchaseOrder != null &&
                        x.PurchaseOrderId ==
                        material.PurchaseOrderId)
                    .ToListAsync();

            var candidateMaterials =
                samePoMaterials
                    .Where(x =>
                        NormalizeKey(
                            x.PurchaseOrder?.PONumber) ==
                        poKey &&
                        NormalizeKey(
                            x.MaterialNo) ==
                        materialKey)
                    .ToList();

            var isSingleCandidate =
                candidateMaterials.Count == 1;

            // --------------------------------------------------------
            // LOAD PENDING ITEMS
            // --------------------------------------------------------

            var pendingItems =
                await _context.PendingInvoiceItems
                    .Include(x => x.Invoice)
                    .Where(x =>
                        x.Status == "Pending")
                    .OrderBy(x => x.Id)
                    .ToListAsync();

            pendingItems =
                pendingItems
                    .Where(x =>
                        NormalizeKey(x.PONumber) ==
                        poKey &&
                        NormalizeKey(x.MaterialNo) ==
                        materialKey)
                    .ToList();

            if (pendingItems.Count == 0)
            {
                return 0;
            }

            var matchedCount = 0;

            foreach (var pending in pendingItems)
            {
                var pendingLineItem =
                    NormalizeLineItem(
                        pending.LineItem);

                var hasExactLineMatch =
                    !string.IsNullOrWhiteSpace(
                        pendingLineItem) &&
                    !string.IsNullOrWhiteSpace(
                        materialLineItem) &&
                    pendingLineItem ==
                    materialLineItem;

                var canUseSingleCandidateFallback =
                    isSingleCandidate &&
                    (string.IsNullOrWhiteSpace(
                         pendingLineItem) ||
                     string.IsNullOrWhiteSpace(
                         materialLineItem));

                if (!hasExactLineMatch &&
                    !canUseSingleCandidateFallback)
                {
                    pending.ErrorMessage =
                        candidateMaterials.Count > 1
                            ? "Multiple material lines found for the same PO and material. Line item is required for safe matching."
                            : "Pending invoice line item does not match this material line item.";

                    _context.PendingInvoiceItems.Update(
                        pending);

                    continue;
                }

                if (pending.InvoiceQty <= 0)
                {
                    pending.ErrorMessage =
                        "Pending invoice qty must be greater than 0.";

                    _context.PendingInvoiceItems.Update(
                        pending);

                    continue;
                }

                // ----------------------------------------------------
                // DUPLICATE PROTECTION
                // ----------------------------------------------------

                var alreadySaved =
                    await _context.InvoiceItems
                        .AnyAsync(x =>
                            x.InvoiceId ==
                            pending.InvoiceId &&
                            x.MaterialId ==
                            material.Id &&
                            x.LineItem ==
                            pending.LineItem &&
                            x.InvoiceQty ==
                            pending.InvoiceQty);

                if (alreadySaved)
                {
                    // Important:
                    // Do not update InvoiceQty or DispatchQty again.
                    pending.Status =
                        "Matched";

                    pending.MatchedMaterialId =
                        material.Id;

                    pending.MatchedDate =
                        DateTime.Now;

                    pending.ErrorMessage =
                        null;

                    _context.PendingInvoiceItems.Update(
                        pending);

                    await MarkImportAuditAsSuccessAsync(
                        pending);

                    continue;
                }

                // ----------------------------------------------------
                // CHECK CURRENT BALANCE
                // ----------------------------------------------------

                var balanceQty =
                    material.BalanceQty > 0
                        ? material.BalanceQty
                        : material.Qty -
                          material.InvoiceQty;

                if (material.InvoiceQty == 0 &&
                    material.BalanceQty == 0)
                {
                    balanceQty =
                        material.Qty;
                }

                if (balanceQty < 0)
                {
                    balanceQty = 0;
                }

                if (pending.InvoiceQty >
                    balanceQty)
                {
                    pending.ErrorMessage =
                        $"Pending invoice qty " +
                        $"{pending.InvoiceQty} is greater " +
                        $"than material balance qty " +
                        $"{balanceQty}.";

                    _context.PendingInvoiceItems.Update(
                        pending);

                    continue;
                }

                // ----------------------------------------------------
                // CREATE NEW INVOICE ITEM
                // ----------------------------------------------------

                _context.InvoiceItems.Add(
                    new InvoiceItem
                    {
                        InvoiceId =
                            pending.InvoiceId,

                        MaterialId =
                            material.Id,

                        PONumber =
                            pending.PONumber,

                        LineItem =
                            string.IsNullOrWhiteSpace(
                                pending.LineItem)
                                ? material.LineItem ?? ""
                                : pending.LineItem,

                        MaterialNo =
                            pending.MaterialNo,

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

                // ----------------------------------------------------
                // UPDATE INVOICE QUANTITY
                // Only run when a NEW InvoiceItem is created.
                // ----------------------------------------------------

                material.InvoiceQty +=
                    pending.InvoiceQty;

                material.BalanceQty =
                    material.Qty -
                    material.InvoiceQty;

                if (material.BalanceQty < 0)
                {
                    material.BalanceQty = 0;
                }

                material.InvoiceStatus =
                    GetInvoiceStatus(
                        material.Qty,
                        material.InvoiceQty,
                        material.BalanceQty);

                // ----------------------------------------------------
                // UPDATE DISPATCH QUANTITY
                // Business rule: Invoice Qty = Dispatch Qty
                // ----------------------------------------------------

                material.DispatchQty +=
                    pending.InvoiceQty;

                material.PendingQty =
                    material.Qty -
                    material.DispatchQty;

                if (material.PendingQty < 0)
                {
                    material.PendingQty = 0;
                }

                material.DispatchDate =
                    pending.InvoiceDate ??
                    DateTime.Today;

                material.DispatchStatus =
                    GetDispatchStatus(
                        material.Qty,
                        material.DispatchQty,
                        material.PendingQty);

                // ----------------------------------------------------
                // UPDATE PENDING ITEM STATUS
                // ----------------------------------------------------

                pending.Status =
                    "Matched";

                pending.MatchedMaterialId =
                    material.Id;

                pending.MatchedDate =
                    DateTime.Now;

                pending.ErrorMessage =
                    null;

                _context.Materials.Update(
                    material);

                _context.PendingInvoiceItems.Update(
                    pending);

                await MarkImportAuditAsSuccessAsync(
                    pending);

                matchedCount++;
            }

            return matchedCount;
        }

        // ============================================================
        // UPDATE IMPORT AUDIT ROW AFTER AUTO MATCH
        // ============================================================

        private async Task
            MarkImportAuditAsSuccessAsync(
                PendingInvoiceItem pending)
        {
            var normalizedLineItem =
                NormalizeLineItem(
                    pending.LineItem);

            var invoiceNumber =
                pending.InvoiceNumber
                    ?.Trim() ?? "";

            var poKey =
                NormalizeKey(
                    pending.PONumber);

            var materialKey =
                NormalizeKey(
                    pending.MaterialNo);

            var auditRows =
                await _context
                    .VyaparInvoiceImportRows
                    .Where(x =>
                        x.Status == "Pending" &&
                        x.InvoiceNumber ==
                        invoiceNumber)
                    .ToListAsync();

            var matchingAuditRows =
                auditRows
                    .Where(x =>
                        NormalizeKey(
                            x.PONumber) ==
                        poKey &&
                        NormalizeKey(
                            x.MaterialNumber) ==
                        materialKey &&
                        NormalizeLineItem(
                            x.LineItem) ==
                        normalizedLineItem &&
                        x.InvoiceQty ==
                        pending.InvoiceQty)
                    .ToList();

            foreach (var auditRow in
                     matchingAuditRows)
            {
                auditRow.Status =
                    "Success";

                auditRow.ErrorMessage =
                    "Automatically matched after PO/material became available.";

                _context
                    .VyaparInvoiceImportRows
                    .Update(auditRow);
            }
        }

        // ============================================================
        // NORMALIZE PO AND MATERIAL KEYS
        // ============================================================

        private static string NormalizeKey(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            return Regex.Replace(
                    value.Trim(),
                    @"[^A-Za-z0-9]",
                    "")
                .ToUpperInvariant();
        }

        // ============================================================
        // NORMALIZE LINE ITEM
        // ============================================================

        private static string NormalizeLineItem(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            value =
                value.Trim();

            value =
                Regex.Replace(
                    value,
                    @"[^0-9]",
                    "");

            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            return "N-" + value;
        }

        // ============================================================
        // INVOICE STATUS
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

            if (balanceQty <= 0 ||
                invoiceQty >= poQty)
            {
                return "Fully Invoiced";
            }

            return "Partially Invoiced";
        }

        // ============================================================
        // DISPATCH STATUS
        // ============================================================

        private static string GetDispatchStatus(
            decimal poQty,
            decimal dispatchQty,
            decimal pendingQty)
        {
            if (dispatchQty <= 0)
            {
                return "Pending";
            }

            if (pendingQty <= 0 ||
                dispatchQty >= poQty)
            {
                return "Dispatched";
            }

            return "Partial";
        }
    }
}
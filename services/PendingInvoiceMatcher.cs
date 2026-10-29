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

        public async Task<int> MatchAllPendingInvoicesAsync(CancellationToken cancellationToken = default)
        {
            var pendingKeys = (await _context.PendingInvoiceItems.AsNoTracking()
                .Where(x => x.Status == "Pending")
                .Select(x => new { x.PONumber, x.MaterialNo })
                .ToListAsync(cancellationToken))
                .Select(x => (NormalizeKey(x.PONumber), NormalizeKey(x.MaterialNo)))
                .ToHashSet();
            if (pendingKeys.Count == 0) return 0;

            var candidates = await _context.Materials.AsNoTracking()
                .Where(x => x.PurchaseOrder != null)
                .Select(x => new { x.Id, x.PurchaseOrder!.PONumber, x.MaterialNo })
                .ToListAsync(cancellationToken);
            var matched = 0;
            foreach (var candidate in candidates.Where(x =>
                pendingKeys.Contains((NormalizeKey(x.PONumber), NormalizeKey(x.MaterialNo)))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Lock the balance and pending rows together during each match.
                await using var transaction = await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken);
                matched += await MatchPendingInvoicesForMaterialAsync(candidate.Id);
                await transaction.CommitAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
            return matched;
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

            // Reuse these tracked rows for every material in the PO. Loading
            // them inside the loop caused one full pending-row query and one
            // same-PO material query per material.
            var pendingItems =
                await _context.PendingInvoiceItems
                    .Where(item => item.Status == "Pending")
                    .OrderBy(item => item.Id)
                    .ToListAsync();

            var totalMatched = 0;

            foreach (var material in materials)
            {
                totalMatched +=
                    await MatchPendingInvoicesForMaterialInternalAsync(
                        material,
                        materials,
                        pendingItems);
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
                Material material,
                List<Material>? samePoMaterials = null,
                List<PendingInvoiceItem>? pendingItemsForPurchaseOrder = null)
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

            samePoMaterials ??=
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

            // --------------------------------------------------------
            // LOAD PENDING ITEMS
            // --------------------------------------------------------

            pendingItemsForPurchaseOrder ??=
                await _context.PendingInvoiceItems
                    .Where(x => x.Status == "Pending")
                    .OrderBy(x => x.Id)
                    .ToListAsync();

            var pendingItems =
                pendingItemsForPurchaseOrder
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
                    materialLineItem &&
                    candidateMaterials.Count(candidate =>
                        NormalizeLineItem(candidate.LineItem) == pendingLineItem) == 1;

                if (!hasExactLineMatch)
                {
                    pending.ErrorMessage =
                        candidateMaterials.Count > 1
                            ? "Multiple material lines found for the same PO and material. Line item is required for safe matching."
                            : "Pending invoice line item does not match this material line item.";

                    continue;
                }

                if (pending.InvoiceQty <= 0)
                {
                    pending.ErrorMessage =
                        "Pending invoice qty must be greater than 0.";

                    continue;
                }

                // ----------------------------------------------------
                // DUPLICATE PROTECTION
                // ----------------------------------------------------

                var savedLineItems =
                    await _context.InvoiceItems
                        .Where(x =>
                            x.InvoiceId == pending.InvoiceId &&
                            x.MaterialId == material.Id &&
                            x.InvoiceQty == pending.InvoiceQty)
                        .Select(x => x.LineItem)
                        .ToListAsync();

                var resolvedLineKey =
                    string.IsNullOrWhiteSpace(pendingLineItem)
                        ? materialLineItem
                        : pendingLineItem;

                var alreadySaved = savedLineItems.Any(savedLine =>
                    NormalizeLineItem(savedLine) == resolvedLineKey) ||
                    _context.InvoiceItems.Local.Any(item =>
                        item.InvoiceId == pending.InvoiceId &&
                        item.MaterialId == material.Id &&
                        item.InvoiceQty == pending.InvoiceQty &&
                        NormalizeLineItem(item.LineItem) == resolvedLineKey);

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

                    await MarkImportAuditAsSuccessAsync(
                        pending);

                    continue;
                }

                // ----------------------------------------------------
                // CHECK CURRENT BALANCE
                // ----------------------------------------------------

                var balanceQty = Math.Max(
                    0m, material.Qty - material.InvoiceQty);

                if (pending.InvoiceQty >
                    balanceQty)
                {
                    pending.ErrorMessage =
                        $"Pending invoice qty " +
                        $"{pending.InvoiceQty} is greater " +
                        $"than material balance qty " +
                        $"{balanceQty}.";

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

            return value;
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

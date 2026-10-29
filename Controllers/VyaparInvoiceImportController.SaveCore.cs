using Microsoft.EntityFrameworkCore;
using POTracker.Models;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class VyaparInvoiceImportController
    {
        private sealed class InvoiceImportSaveOperationResult
        {
            public bool IsSuccessful { get; init; }

            public string Message { get; init; } =
                string.Empty;

            public int InvoiceId { get; init; }

            public string InvoiceNumber { get; init; } =
                string.Empty;

            public int TotalRows { get; init; }

            public int SuccessRows { get; init; }

            public int PendingRows { get; init; }
        }

        // ============================================================
        // SHARED SINGLE INVOICE DATABASE SAVE
        // ============================================================

        private async Task<InvoiceImportSaveOperationResult>
            SaveInvoiceAsync(
                InvoiceImportPreviewViewModel model)
        {
            var items =
                model.Items;

            if (items == null ||
                items.Count == 0)
            {
                return new InvoiceImportSaveOperationResult
                {
                    IsSuccessful = false,
                    Message =
                        "No invoice items found to save."
                };
            }

            var invoiceNumber =
                model.InvoiceNumber
                    ?.Trim() ?? string.Empty;

            var uploadedFileName =
                model.UploadedFileName
                    ?.Trim() ?? string.Empty;

            if (IsInvalidInvoiceNumber(
                    invoiceNumber) ||
                invoiceNumber.StartsWith(
                    "REVIEW-",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new InvoiceImportSaveOperationResult
                {
                    IsSuccessful = false,
                    Message =
                        "Enter the correct invoice number before saving.",
                    InvoiceNumber =
                        invoiceNumber,
                    TotalRows =
                        items.Count
                };
            }

            if (string.IsNullOrWhiteSpace(
                    uploadedFileName))
            {
                uploadedFileName =
                    "Invoice_" +
                    DateTime.Now.ToString(
                        "yyyyMMddHHmmss");
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var batch =
                    new VyaparInvoiceImportBatch
                    {
                        FileName =
                            uploadedFileName,

                        ImportDate =
                            DateTime.Now,

                        TotalRows =
                            items.Count,

                        SuccessRows =
                            0,

                        // Failed status is not used.
                        FailedRows =
                            0
                    };

                _context
                    .VyaparInvoiceImportBatches
                    .Add(batch);

                await _context.SaveChangesAsync();

                var invoice =
                    await _context.Invoices
                        .FirstOrDefaultAsync(x =>
                            x.InvoiceNumber ==
                            invoiceNumber);

                if (invoice == null)
                {
                    invoice =
                        new Invoice
                        {
                            InvoiceNumber =
                                invoiceNumber,

                            InvoiceDate =
                                model.InvoiceDate,

                            CustomerName =
                                model.CustomerName,

                            UploadedFileName =
                                uploadedFileName,

                            UploadedFilePath =
                                model.UploadedFilePath,

                            CreatedDate =
                                DateTime.Now
                        };

                    _context.Invoices.Add(
                        invoice);
                }
                else
                {
                    invoice.InvoiceDate =
                        model.InvoiceDate ??
                        invoice.InvoiceDate;

                    invoice.CustomerName =
                        string.IsNullOrWhiteSpace(
                            model.CustomerName)
                            ? invoice.CustomerName
                            : model.CustomerName;

                    invoice.UploadedFileName =
                        uploadedFileName;

                    invoice.UploadedFilePath =
                        string.IsNullOrWhiteSpace(
                            model.UploadedFilePath)
                            ? invoice.UploadedFilePath
                            : model.UploadedFilePath;

                    _context.Invoices.Update(
                        invoice);
                }

                await _context.SaveChangesAsync();

                var existingSuccessRows =
                    await _context
                        .VyaparInvoiceImportRows
                        .Where(x =>
                            x.InvoiceNumber ==
                                invoiceNumber &&
                            x.Status ==
                                "Success")
                        .ToListAsync();

                var existingPendingItems =
                    await _context
                        .PendingInvoiceItems
                        .Where(x =>
                            x.InvoiceId ==
                                invoice.Id &&
                            x.Status ==
                                "Pending")
                        .ToListAsync();

                var currentUploadKeys =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                var currentPendingKeys =
                    existingPendingItems
                        .Select(x =>
                            BuildInvoiceRowKey(
                                x.PONumber,
                                x.MaterialNo,
                                x.LineItem,
                                x.InvoiceQty))
                        .ToHashSet(
                            StringComparer.OrdinalIgnoreCase);

                var successRows = 0;
                var pendingRows = 0;

                foreach (var item in items)
                {
                    var poNumber =
                        item.PONumber
                            ?.Trim() ?? "";

                    var materialNo =
                        item.MaterialNo
                            ?.Trim() ?? "";

                    var lineItem =
                        NormalizeLineItem(
                            item.LineItem);

                    var invoiceQty =
                        item.InvoiceQty;

                    var poKey =
                        NormalizeKey(
                            poNumber);

                    var materialKey =
                        NormalizeKey(
                            materialNo);

                    var uploadKey =
                        $"{poKey}|{materialKey}|{lineItem}";

                    string? pendingReason =
                        null;

                    var storePendingItem =
                        true;

                    // ================================================
                    // ALL NON-SUCCESS RESULTS BECOME PENDING
                    // ================================================

                    if (string.IsNullOrWhiteSpace(
                            poNumber))
                    {
                        pendingReason =
                            "PO Number is missing. Stored as Pending.";
                    }
                    else if (string.IsNullOrWhiteSpace(
                                 materialNo))
                    {
                        pendingReason =
                            "Material Number is missing. Stored as Pending.";
                    }
                    else if (invoiceQty <= 0)
                    {
                        pendingReason =
                            "Invoice Qty must be greater than 0. Stored as Pending.";
                    }
                    else if (!currentUploadKeys.Add(
                                 uploadKey))
                    {
                        pendingReason =
                            "Duplicate PO, material and line item found inside this upload. Stored as Pending for review.";

                        // Do not create another identical
                        // PendingInvoiceItem.
                        storePendingItem =
                            false;
                    }

                    if (pendingReason != null)
                    {
                        if (storePendingItem)
                        {
                            AddPendingInvoiceItem(
                                invoice,
                                model,
                                item,
                                poNumber,
                                lineItem,
                                materialNo,
                                pendingReason,
                                currentPendingKeys);
                        }

                        AddImportRow(
                            batch.Id,
                            poNumber,
                            lineItem,
                            materialNo,
                            invoiceQty,
                            invoiceNumber,
                            "Pending",
                            pendingReason);

                        pendingRows++;

                        continue;
                    }

                    // ================================================
                    // FIND PO + MATERIAL
                    // ================================================

                    var candidates =
                        await _context.Materials
                            .Include(x =>
                                x.PurchaseOrder)
                            .Where(x =>
                                x.PurchaseOrder != null &&
                                x.PurchaseOrder!.PONumber
                                    .Trim()
                                    .ToUpper() ==
                                    poKey &&
                                (x.MaterialNo ?? "")
                                    .Trim()
                                    .ToUpper() ==
                                    materialKey)
                            .OrderBy(x =>
                                x.Id)
                            .ToListAsync();

                    Material? material =
                        null;

                    if (candidates.Count == 0)
                    {
                        pendingReason =
                            "PO and material combination was not found.";
                    }
                    else if (string.IsNullOrWhiteSpace(
                                 lineItem))
                    {
                        if (candidates.Count == 1)
                        {
                            material =
                                candidates[0];
                        }
                        else
                        {
                            pendingReason =
                                "Multiple material lines were found. Line item is required for safe matching.";
                        }
                    }
                    else
                    {
                        var exactLineCandidates =
                            candidates
                                .Where(x =>
                                    NormalizeLineItem(
                                        x.LineItem) ==
                                    lineItem)
                                .ToList();

                        if (exactLineCandidates.Count == 1)
                        {
                            material =
                                exactLineCandidates[0];
                        }
                        else if (exactLineCandidates.Count > 1)
                        {
                            pendingReason =
                                "Multiple materials have the same PO, material and line item. Manual review is required.";
                        }
                        else if (
                            candidates.Count == 1 &&
                            string.IsNullOrWhiteSpace(
                                NormalizeLineItem(
                                    candidates[0].LineItem)))
                        {
                            material =
                                candidates[0];
                        }
                        else
                        {
                            pendingReason =
                                "PO and material were found, but the invoice line item does not match.";
                        }
                    }

                    if (material == null)
                    {
                        pendingReason ??=
                            "No safe material match was found.";

                        AddPendingInvoiceItem(
                            invoice,
                            model,
                            item,
                            poNumber,
                            lineItem,
                            materialNo,
                            pendingReason,
                            currentPendingKeys);

                        AddImportRow(
                            batch.Id,
                            poNumber,
                            lineItem,
                            materialNo,
                            invoiceQty,
                            invoiceNumber,
                            "Pending",
                            pendingReason);

                        pendingRows++;

                        continue;
                    }

                    var resolvedLineItem =
                        string.IsNullOrWhiteSpace(
                            lineItem)
                            ? NormalizeLineItem(
                                material.LineItem)
                            : lineItem;

                    var existingInvoiceItems =
                        await _context.InvoiceItems
                            .Where(x =>
                                x.InvoiceId == invoice.Id &&
                                x.MaterialId == material.Id)
                            .ToListAsync();

                    var matchingInvoiceItems = existingInvoiceItems
                        .Where(x => NormalizeLineItem(x.LineItem) == resolvedLineItem)
                        .ToList();

                    if (matchingInvoiceItems.Count > 1)
                    {
                        pendingReason =
                            "Multiple saved invoice lines have this PO, material and numeric line. Manual review is required.";
                        AddImportRow(batch.Id, poNumber, resolvedLineItem,
                            materialNo, invoiceQty, invoiceNumber, "Pending", pendingReason);
                        pendingRows++;
                        continue;
                    }

                    if (matchingInvoiceItems.Count == 1)
                    {
                        var existingInvoiceItem = matchingInvoiceItems[0];
                        var oldQuantity = existingInvoiceItem.InvoiceQty;
                        var quantityDelta = invoiceQty - oldQuantity;
                        var availableForUpdate = Math.Max(
                            0m, material.Qty - material.InvoiceQty + oldQuantity);

                        if (invoiceQty <= 0 || invoiceQty > availableForUpdate)
                        {
                            pendingReason =
                                $"Updated invoice quantity {invoiceQty} exceeds the available PO balance {availableForUpdate}.";
                            AddImportRow(batch.Id, poNumber, resolvedLineItem,
                                materialNo, invoiceQty, invoiceNumber, "Pending", pendingReason);
                            pendingRows++;
                            continue;
                        }

                        existingInvoiceItem.PONumber = poNumber;
                        existingInvoiceItem.LineItem = resolvedLineItem;
                        existingInvoiceItem.MaterialNo = materialNo;
                        existingInvoiceItem.Description = item.Description;
                        existingInvoiceItem.HsnSacCode = item.HsnSacCode;
                        existingInvoiceItem.InvoiceQty = invoiceQty;
                        existingInvoiceItem.PriceUnit = item.PriceUnit;
                        existingInvoiceItem.GstAmount = item.GstAmount;
                        existingInvoiceItem.Amount = item.Amount;

                        material.InvoiceQty = Math.Max(0m, material.InvoiceQty + quantityDelta);
                        material.BalanceQty = Math.Max(0m, material.Qty - material.InvoiceQty);
                        material.InvoiceStatus = GetInvoiceStatus(
                            material.Qty, material.InvoiceQty, material.BalanceQty);
                        material.DispatchQty = Math.Max(0m, material.DispatchQty + quantityDelta);
                        material.PendingQty = Math.Max(0m, material.Qty - material.DispatchQty);
                        material.DispatchStatus = GetDispatchStatus(
                            material.Qty, material.DispatchQty, material.PendingQty);
                        _context.Materials.Update(material);

                        AddImportRow(batch.Id, poNumber, resolvedLineItem,
                            materialNo, invoiceQty, invoiceNumber, "Success", null);
                        successRows++;
                        continue;
                    }

                    var balanceQty = Math.Max(
                        0m, material.Qty - material.InvoiceQty);

                    if (invoiceQty >
                        balanceQty)
                    {
                        pendingReason =
                            $"Invoice Qty {invoiceQty} is greater " +
                            $"than balance qty {balanceQty}. " +
                            "Stored as Pending.";

                        AddPendingInvoiceItem(
                            invoice,
                            model,
                            item,
                            poNumber,
                            resolvedLineItem,
                            materialNo,
                            pendingReason,
                            currentPendingKeys);

                        AddImportRow(
                            batch.Id,
                            poNumber,
                            resolvedLineItem,
                            materialNo,
                            invoiceQty,
                            invoiceNumber,
                            "Pending",
                            pendingReason);

                        pendingRows++;

                        continue;
                    }

                    // ================================================
                    // SUCCESS
                    // ================================================

                    _context.InvoiceItems.Add(
                        new InvoiceItem
                        {
                            InvoiceId =
                                invoice.Id,

                            MaterialId =
                                material.Id,

                            PONumber =
                                poNumber,

                            LineItem =
                                resolvedLineItem,

                            MaterialNo =
                                materialNo,

                            Description =
                                item.Description,

                            HsnSacCode =
                                item.HsnSacCode,

                            InvoiceQty =
                                invoiceQty,

                            PriceUnit =
                                item.PriceUnit,

                            GstAmount =
                                item.GstAmount,

                            Amount =
                                item.Amount,

                            CreatedDate =
                                DateTime.Now
                        });

                    material.InvoiceQty +=
                        invoiceQty;

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

                    material.DispatchQty +=
                        invoiceQty;

                    material.PendingQty =
                        material.Qty -
                        material.DispatchQty;

                    if (material.PendingQty < 0)
                    {
                        material.PendingQty = 0;
                    }

                    material.DispatchDate =
                        model.InvoiceDate ??
                        DateTime.Today;

                    material.DispatchStatus =
                        GetDispatchStatus(
                            material.Qty,
                            material.DispatchQty,
                            material.PendingQty);

                    _context.Materials.Update(
                        material);

                    AddImportRow(
                        batch.Id,
                        poNumber,
                        resolvedLineItem,
                        materialNo,
                        invoiceQty,
                        invoiceNumber,
                        "Success",
                        null);

                    successRows++;
                }

                batch.SuccessRows =
                    successRows;

                // No Failed status is used.
                batch.FailedRows =
                    0;

                _context
                    .VyaparInvoiceImportBatches
                    .Update(batch);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new InvoiceImportSaveOperationResult
                {
                    IsSuccessful =
                        true,

                    Message =
                        $"Invoice import completed. " +
                        $"Total: {items.Count}, " +
                        $"Success: {successRows}, " +
                        $"Pending: {pendingRows}.",

                    InvoiceId =
                        invoice.Id,

                    InvoiceNumber =
                        invoiceNumber,

                    TotalRows =
                        items.Count,

                    SuccessRows =
                        successRows,

                    PendingRows =
                        pendingRows
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return new InvoiceImportSaveOperationResult
                {
                    IsSuccessful =
                        false,

                    Message =
                        "Invoice import failed and no partial changes " +
                        "were saved. Error: " +
                        ex.Message,

                    InvoiceNumber =
                        invoiceNumber,

                    TotalRows =
                        items.Count
                };
            }
        }
        // ============================================================
        // SHARED MULTIPLE INVOICE DATABASE SAVE
        // ============================================================

        private async Task<(
            InvoiceBatchImportResultViewModel? Result,
            string? ErrorMessage)>
            SaveInvoiceBatchAsync(
                InvoiceBatchImportPreviewViewModel model)
        {
            if (model.Files == null ||
                model.Files.Count == 0)
            {
                return (
                    null,
                    "No bulk invoice files were available to save.");
            }

            var selectedFiles =
                model.Files
                    .Where(file =>
                        file.IsSelected &&
                        file.IsSuccessful &&
                        !file.IsDuplicateInBatch &&
                        !file.ExistsInDatabase &&
                        file.Invoice != null)
                    .ToList();

            if (selectedFiles.Count == 0)
            {
                return (
                    null,
                    "Please select at least one valid invoice file.");
            }

            if (selectedFiles.Count >
                MaxBatchFileCount)
            {
                return (
                    null,
                    $"A maximum of {MaxBatchFileCount} invoices " +
                    "can be saved in one batch.");
            }

            var selectedInvoiceNumbers =
                selectedFiles
                    .Select(file =>
                        file.Invoice!
                            .InvoiceNumber
                            ?.Trim() ??
                        string.Empty)
                    .Where(invoiceNumber =>
                        !IsInvalidInvoiceNumber(
                            invoiceNumber) &&
                        !invoiceNumber.StartsWith(
                            "REVIEW-",
                            StringComparison.OrdinalIgnoreCase))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var existingInvoiceNumbers =
                selectedInvoiceNumbers.Count == 0
                    ? new List<string>()
                    : await _context.Invoices
                        .AsNoTracking()
                        .Where(invoice =>
                            selectedInvoiceNumbers.Contains(
                                invoice.InvoiceNumber))
                        .Select(invoice =>
                            invoice.InvoiceNumber)
                        .ToListAsync();

            var existingInvoiceNumberSet =
                existingInvoiceNumbers.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            var resultModel =
                new InvoiceBatchImportResultViewModel
                {
                    BatchId =
                        model.BatchId
                };

            var processedInvoiceNumbers =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var file in selectedFiles)
            {
                var invoiceModel =
                    file.Invoice!;

                var invoiceNumber =
                    invoiceModel.InvoiceNumber
                        ?.Trim() ??
                    string.Empty;

                var itemResult =
                    new InvoiceBatchImportResultItemViewModel
                    {
                        OriginalFileName =
                            file.OriginalFileName,

                        InvoiceNumber =
                            invoiceNumber
                    };

                resultModel.Results.Add(
                    itemResult);

                if (IsInvalidInvoiceNumber(
                        invoiceNumber) ||
                    invoiceNumber.StartsWith(
                        "REVIEW-",
                        StringComparison.OrdinalIgnoreCase))
                {
                    itemResult.Message =
                        "A valid invoice number is required.";

                    continue;
                }

                if (!processedInvoiceNumbers.Add(
                        invoiceNumber))
                {
                    itemResult.Message =
                        "Duplicate invoice number in this batch.";

                    continue;
                }

                if (invoiceModel.Items == null ||
                    invoiceModel.Items.Count == 0)
                {
                    itemResult.Message =
                        "No invoice rows were available to save.";

                    continue;
                }

                if (existingInvoiceNumberSet.Contains(
                        invoiceNumber))
                {
                    itemResult.Message =
                        "This invoice number already exists in the database.";

                    continue;
                }

                try
                {
                    var saveResult =
                        await SaveInvoiceAsync(
                            invoiceModel);

                    itemResult.IsImported =
                        saveResult.IsSuccessful;

                    itemResult.RowsSaved =
                        saveResult.SuccessRows;

                    itemResult.Message =
                        saveResult.IsSuccessful
                            ? $"Imported successfully. " +
                              $"Success: {saveResult.SuccessRows}, " +
                              $"Pending: {saveResult.PendingRows}."
                            : saveResult.Message;
                }
                catch (Exception ex)
                {
                    itemResult.Message =
                        "Invoice save failed. " +
                        ex.Message;
                }
            }

            return (
                resultModel,
                null);
        }
    }
}
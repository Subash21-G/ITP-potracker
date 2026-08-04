using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class VyaparInvoiceImportController
    {
        // ============================================================
        // SHARED SINGLE INVOICE FILE PREVIEW
        // ============================================================

        private async Task<InvoiceBatchImportFileViewModel>
            ProcessSingleInvoiceFileAsync(
                IFormFile? file)
        {
            var result =
                new InvoiceBatchImportFileViewModel
                {
                    OriginalFileName =
                        file == null
                            ? string.Empty
                            : Path.GetFileName(
                                file.FileName),

                    FileSize =
                        file?.Length ?? 0,

                    IsSelected =
                        false
                };

            if (file == null ||
                file.Length == 0)
            {
                result.ErrorMessage =
                    "Please select an invoice PDF or Excel file.";

                return result;
            }

            if (file.Length >
                MaxInvoiceFileSize)
            {
                result.ErrorMessage =
                    "The invoice file exceeds the 20 MB limit.";

                return result;
            }

            var extension =
                Path.GetExtension(
                    file.FileName)
                    .ToLowerInvariant();

            if (extension != ".pdf" &&
                extension != ".xlsx" &&
                extension != ".xls")
            {
                result.ErrorMessage =
                    "Only PDF, XLSX and XLS invoice files are allowed.";

                return result;
            }

            var uploadFolder =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "invoices");

            Directory.CreateDirectory(
                uploadFolder);

            var safeFileName =
                Path.GetFileName(
                    file.FileName);

            var storedFileName =
                $"{Guid.NewGuid():N}_{safeFileName}";

            var physicalPath =
                Path.Combine(
                    uploadFolder,
                    storedFileName);

            string? privateRelativePath =
                null;

            try
            {
                await using (
                    var stream =
                        new FileStream(
                            physicalPath,
                            FileMode.CreateNew))
                {
                    await file.CopyToAsync(
                        stream);
                }

                var invoiceModel =
                    extension == ".pdf"
                        ? ExtractFromPdf(
                            physicalPath)
                        : ExtractFromExcel(
                            physicalPath);

                invoiceModel.UploadedFileName =
                    safeFileName;


                if (IsInvalidInvoiceNumber(
                        invoiceModel.InvoiceNumber))
                {
                    invoiceModel.InvoiceNumber =
                        GetInvoiceNumberFromFileName(
                            safeFileName);

                    if (IsInvalidInvoiceNumber(
                            invoiceModel.InvoiceNumber))
                    {
                        invoiceModel.InvoiceNumber =
                            "REVIEW-" +
                            Guid.NewGuid()
                                .ToString("N")[..12]
                                .ToUpperInvariant();
                    }
                }

                if (invoiceModel.Items == null ||
                    invoiceModel.Items.Count == 0)
                {
                    if (System.IO.File.Exists(
                            physicalPath))
                    {
                        System.IO.File.Delete(
                            physicalPath);
                    }

                    result.ErrorMessage =
                        "No invoice items were detected in this file.";

                    return result;
                }

                privateRelativePath =
                    await MirrorInvoiceToPrivateStorageAsync(
                        physicalPath,
                        storedFileName);

                invoiceModel.UploadedFilePath =
                    _documentReferences.ToDownloadUrl(
                        privateRelativePath,
                        "invoices") ??
                    throw new InvalidOperationException(
                        "Unable to create the secure invoice URL.");

                var invoiceNumber =
                    invoiceModel.InvoiceNumber
                        .Trim();

                result.ExistsInDatabase =
                    await _context.Invoices
                        .AsNoTracking()
                        .AnyAsync(invoice =>
                            invoice.InvoiceNumber ==
                            invoiceNumber);

                result.Invoice =
                    invoiceModel;

                result.IsSuccessful =
                    true;

                result.IsSelected =
                    !result.ExistsInDatabase;

                return result;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(
                        privateRelativePath))
                {
                    _privateDocumentStorage.DeleteIfExists(
                        privateRelativePath);
                }

                if (System.IO.File.Exists(
                        physicalPath))
                {
                    System.IO.File.Delete(
                        physicalPath);
                }

                result.ErrorMessage =
                    "Unable to read this invoice file. " +
                    ex.Message;

                return result;
            }
        }
        private async Task<string>
            MirrorInvoiceToPrivateStorageAsync(
                string publicPhysicalPath,
                string storedFileName)
        {
            await using var source =
                new FileStream(
                    publicPhysicalPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    options:
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);

            var privateDocument =
                await _privateDocumentStorage
                    .SaveWithStoredFileNameAsync(
                        source,
                        "invoices",
                        storedFileName);

            RegisterTemporaryInvoicePreview(
                privateDocument.RelativePath);

            return privateDocument.RelativePath;
        }
        // ============================================================
        // SHARED MULTIPLE INVOICE FILE PREVIEW
        // ============================================================

        private async Task<(
            InvoiceBatchImportPreviewViewModel? Preview,
            string? ErrorMessage)>
            ProcessInvoiceBatchFilesAsync(
                List<IFormFile>? invoiceFiles)
        {
            if (invoiceFiles == null ||
                invoiceFiles.Count == 0)
            {
                return (
                    null,
                    "Please select one or more invoice files.");
            }

            if (invoiceFiles.Count >
                MaxBatchFileCount)
            {
                return (
                    null,
                    $"You can upload a maximum of " +
                    $"{MaxBatchFileCount} invoice files at one time.");
            }

            var totalSize =
                invoiceFiles.Sum(file =>
                    file?.Length ?? 0);

            if (totalSize >
                MaxBatchUploadSize)
            {
                return (
                    null,
                    "The combined upload size cannot exceed 200 MB.");
            }

            var batchModel =
                new InvoiceBatchImportPreviewViewModel();

            var uploadFolder =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "invoices");

            Directory.CreateDirectory(
                uploadFolder);

            foreach (var invoiceFile in invoiceFiles)
            {
                var fileResult =
                    new InvoiceBatchImportFileViewModel
                    {
                        OriginalFileName =
                            invoiceFile == null
                                ? string.Empty
                                : Path.GetFileName(
                                    invoiceFile.FileName),

                        FileSize =
                            invoiceFile?.Length ?? 0,

                        IsSelected =
                            false
                    };

                batchModel.Files.Add(
                    fileResult);

                if (invoiceFile == null ||
                    invoiceFile.Length == 0)
                {
                    fileResult.ErrorMessage =
                        "The selected invoice file is empty.";

                    continue;
                }

                if (invoiceFile.Length >
                    MaxInvoiceFileSize)
                {
                    fileResult.ErrorMessage =
                        "The invoice file exceeds the 20 MB limit.";

                    continue;
                }

                var extension =
                    Path.GetExtension(
                        invoiceFile.FileName)
                        .ToLowerInvariant();

                if (extension != ".pdf" &&
                    extension != ".xlsx" &&
                    extension != ".xls")
                {
                    fileResult.ErrorMessage =
                        "Only PDF, XLSX and XLS invoice files are allowed.";

                    continue;
                }

                var safeFileName =
                    Path.GetFileName(
                        invoiceFile.FileName);

                var storedFileName =
                    $"{Guid.NewGuid():N}_{safeFileName}";

                var physicalPath =
                    Path.Combine(
                        uploadFolder,
                        storedFileName);

                string? privateRelativePath =
                    null;

                try
                {
                    await using (
                        var stream =
                            new FileStream(
                                physicalPath,
                                FileMode.CreateNew))
                    {
                        await invoiceFile.CopyToAsync(
                            stream);
                    }

                    var invoiceModel =
                        extension == ".pdf"
                            ? ExtractFromPdf(
                                physicalPath)
                            : ExtractFromExcel(
                                physicalPath);

                    invoiceModel.UploadedFileName =
                        safeFileName;


                    if (IsInvalidInvoiceNumber(
                            invoiceModel.InvoiceNumber))
                    {
                        invoiceModel.InvoiceNumber =
                            GetInvoiceNumberFromFileName(
                                safeFileName);

                        if (IsInvalidInvoiceNumber(
                                invoiceModel.InvoiceNumber))
                        {
                            invoiceModel.InvoiceNumber =
                                "REVIEW-" +
                                Guid.NewGuid()
                                    .ToString("N")[..12]
                                    .ToUpperInvariant();
                        }
                    }

                    if (invoiceModel.Items == null ||
                        invoiceModel.Items.Count == 0)
                    {
                        if (System.IO.File.Exists(
                                physicalPath))
                        {
                            System.IO.File.Delete(
                                physicalPath);
                        }

                        fileResult.ErrorMessage =
                            "No invoice items were detected in this file.";

                        continue;
                    }

                    privateRelativePath =
                        await MirrorInvoiceToPrivateStorageAsync(
                            physicalPath,
                            storedFileName);

                    invoiceModel.UploadedFilePath =
                        _documentReferences.ToDownloadUrl(
                            privateRelativePath,
                            "invoices") ??
                        throw new InvalidOperationException(
                            "Unable to create the secure invoice URL.");

                    fileResult.Invoice =
                        invoiceModel;

                    fileResult.IsSuccessful =
                        true;

                    fileResult.IsSelected =
                        true;
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrWhiteSpace(
                            privateRelativePath))
                    {
                        _privateDocumentStorage.DeleteIfExists(
                            privateRelativePath);
                    }

                    if (System.IO.File.Exists(
                            physicalPath))
                    {
                        System.IO.File.Delete(
                            physicalPath);
                    }

                    fileResult.ErrorMessage =
                        "Unable to read this invoice file. " +
                        ex.Message;
                }
            }

            var extractedInvoiceNumbers =
                batchModel.Files
                    .Where(file =>
                        file.IsSuccessful &&
                        file.Invoice != null &&
                        !string.IsNullOrWhiteSpace(
                            file.Invoice.InvoiceNumber))
                    .Select(file =>
                        file.Invoice!
                            .InvoiceNumber
                            .Trim())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var existingInvoiceNumbers =
                extractedInvoiceNumbers.Count == 0
                    ? new List<string>()
                    : await _context.Invoices
                        .AsNoTracking()
                        .Where(invoice =>
                            extractedInvoiceNumbers.Contains(
                                invoice.InvoiceNumber))
                        .Select(invoice =>
                            invoice.InvoiceNumber)
                        .ToListAsync();

            var existingInvoiceNumberSet =
                existingInvoiceNumbers.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            foreach (
                var fileResult in
                batchModel.Files.Where(file =>
                    file.IsSuccessful &&
                    file.Invoice != null))
            {
                var invoiceNumber =
                    fileResult.Invoice!
                        .InvoiceNumber
                        .Trim();

                fileResult.ExistsInDatabase =
                    existingInvoiceNumberSet.Contains(
                        invoiceNumber);

                fileResult.IsSelected =
                    !fileResult.ExistsInDatabase;
            }

            var duplicateGroups =
                batchModel.Files
                    .Where(file =>
                        file.IsSuccessful &&
                        file.Invoice != null &&
                        !string.IsNullOrWhiteSpace(
                            file.Invoice.InvoiceNumber))
                    .GroupBy(
                        file =>
                            file.Invoice!
                                .InvoiceNumber
                                .Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Where(group =>
                        group.Count() > 1)
                    .ToList();

            foreach (
                var duplicateGroup in
                duplicateGroups)
            {
                foreach (
                    var duplicateFile in
                    duplicateGroup.Skip(1))
                {
                    duplicateFile.IsDuplicateInBatch =
                        true;

                    duplicateFile.IsSelected =
                        false;

                    duplicateFile.ErrorMessage =
                        "The same invoice number is already present " +
                        "in another selected file.";
                }
            }

            return (
                batchModel,
                null);
        }
    }
}
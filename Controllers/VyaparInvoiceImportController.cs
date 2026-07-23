using ClosedXML.Excel;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using POTracker.ViewModels;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace POTracker.Controllers
{
    public partial class VyaparInvoiceImportController : Controller
    {
        private const int MaxBatchFileCount = 100;

        private const long MaxInvoiceFileSize =
            20L * 1024L * 1024L;

        private const long MaxBatchUploadSize =
            200L * 1024L * 1024L;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public VyaparInvoiceImportController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Upload()
        {
            return View();
        }

        // ============================================================
        // DOWNLOAD INVOICE EXCEL IMPORT TEMPLATE
        // ============================================================

        [HttpGet]
        public IActionResult DownloadExcelTemplate()
        {
            using var workbook = new XLWorkbook();

            // ========================================================
            // MAIN INVOICE IMPORT SHEET
            // ========================================================

            var worksheet =
                workbook.Worksheets.Add("Invoice Import");

            const int headerRow = 6;
            const int maximumDataRows = 500;

            var headers = new[]
            {
                "PO/DC Date",
                "PO/DC NO",
                "ITEM NO",
                "MATERIAL",
                "DESCRIPTION",
                "HSN/SAC",
                "QTY",
                "Price/Unit",
                "GST",
                "Amount"
            };

            // ========================================================
            // TITLE
            // ========================================================

            worksheet.Range("A1:J1").Merge();

            worksheet.Cell("A1").Value =
                "POTracker Invoice Excel Import Template";

            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 16;
            worksheet.Cell("A1").Style.Font.FontColor =
                XLColor.White;

            worksheet.Cell("A1").Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1F4FD7");

            worksheet.Cell("A1").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A1").Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 30;

            // ========================================================
            // INVOICE HEADER FIELDS
            // ========================================================

            worksheet.Cell("A2").Value = "Invoice No";
            worksheet.Cell("A3").Value = "Date";
            worksheet.Cell("A4").Value = "Bill To";

            worksheet.Range("B2:F2").Merge();
            worksheet.Range("B3:F3").Merge();
            worksheet.Range("B4:F4").Merge();

            var invoiceLabelRange =
                worksheet.Range("A2:A4");

            invoiceLabelRange.Style.Font.Bold = true;

            invoiceLabelRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#E8EEFF");

            invoiceLabelRange.Style.Font.FontColor =
                XLColor.FromHtml("#1F3A8A");

            invoiceLabelRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            var invoiceValueRange =
                worksheet.Range("B2:F4");

            invoiceValueRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#FFFDF5");

            invoiceValueRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            invoiceValueRange.Style.Border.InsideBorder =
                XLBorderStyleValues.Thin;

            invoiceValueRange.Style.Border.OutsideBorderColor =
                XLColor.FromHtml("#D8DEE9");

            invoiceValueRange.Style.Border.InsideBorderColor =
                XLColor.FromHtml("#D8DEE9");

            worksheet.Cell("B3").Style.DateFormat.Format =
                "dd-MM-yyyy";

            // ========================================================
            // MATERIAL TABLE HEADERS
            // ========================================================

            for (var columnIndex = 0;
                 columnIndex < headers.Length;
                 columnIndex++)
            {
                worksheet.Cell(
                    headerRow,
                    columnIndex + 1).Value =
                        headers[columnIndex];
            }

            var tableHeaderRange =
                worksheet.Range(
                    headerRow,
                    1,
                    headerRow,
                    headers.Length);

            tableHeaderRange.Style.Font.Bold = true;

            tableHeaderRange.Style.Font.FontColor =
                XLColor.White;

            tableHeaderRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#3346A8");

            tableHeaderRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            tableHeaderRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            tableHeaderRange.Style.Alignment.WrapText = true;

            tableHeaderRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            tableHeaderRange.Style.Border.InsideBorder =
                XLBorderStyleValues.Thin;

            tableHeaderRange.Style.Border.OutsideBorderColor =
                XLColor.White;

            tableHeaderRange.Style.Border.InsideBorderColor =
                XLColor.White;

            worksheet.Row(headerRow).Height = 28;

            // Highlight required headers.
            var requiredColumns = new[]
            {
                2, // PO/DC NO
                4, // MATERIAL
                7  // QTY
            };

            foreach (var columnNumber in requiredColumns)
            {
                worksheet.Cell(
                        headerRow,
                        columnNumber)
                    .Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#B42318");
            }

            // ========================================================
            // EMPTY DATA ENTRY AREA
            // ========================================================

            var firstDataRow =
                headerRow + 1;

            var lastDataRow =
                headerRow + maximumDataRows;

            var dataRange =
                worksheet.Range(
                    firstDataRow,
                    1,
                    lastDataRow,
                    headers.Length);

            dataRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            dataRange.Style.Border.InsideBorder =
                XLBorderStyleValues.Hair;

            dataRange.Style.Border.OutsideBorderColor =
                XLColor.FromHtml("#CBD5E1");

            dataRange.Style.Border.InsideBorderColor =
                XLColor.FromHtml("#E5E7EB");

            dataRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Top;

            // ========================================================
            // COLUMN FORMATS
            // ========================================================

            worksheet.Column(1)
                .Style.DateFormat.Format =
                    "dd-MM-yyyy";

            // Preserve leading zeros in PO, item, material and HSN.
            worksheet.Column(2)
                .Style.NumberFormat.Format = "@";

            worksheet.Column(3)
                .Style.NumberFormat.Format = "@";

            worksheet.Column(4)
                .Style.NumberFormat.Format = "@";

            worksheet.Column(6)
                .Style.NumberFormat.Format = "@";

            worksheet.Column(7)
                .Style.NumberFormat.Format =
                    "0.00";

            worksheet.Column(8)
                .Style.NumberFormat.Format =
                    "#,##0.00";

            worksheet.Column(9)
                .Style.NumberFormat.Format =
                    "#,##0.00";

            worksheet.Column(10)
                .Style.NumberFormat.Format =
                    "#,##0.00";

            // ========================================================
            // COLUMN WIDTHS
            // ========================================================

            worksheet.Column(1).Width = 15;
            worksheet.Column(2).Width = 20;
            worksheet.Column(3).Width = 13;
            worksheet.Column(4).Width = 18;
            worksheet.Column(5).Width = 38;
            worksheet.Column(6).Width = 16;
            worksheet.Column(7).Width = 12;
            worksheet.Column(8).Width = 15;
            worksheet.Column(9).Width = 14;
            worksheet.Column(10).Width = 17;

            worksheet.Column(5)
                .Style.Alignment.WrapText = true;

            // ========================================================
            // FREEZE HEADER AND FILTER
            // ========================================================

            worksheet.SheetView.FreezeRows(headerRow);

            worksheet.Range(
                    headerRow,
                    1,
                    lastDataRow,
                    headers.Length)
                .SetAutoFilter();

            // ========================================================
            // INSTRUCTIONS SHEET
            // ========================================================

            var instructions =
                workbook.Worksheets.Add("Instructions");

            instructions.Range("A1:D1").Merge();

            instructions.Cell("A1").Value =
                "Invoice Excel Import Instructions";

            instructions.Cell("A1").Style.Font.Bold = true;
            instructions.Cell("A1").Style.Font.FontSize = 16;
            instructions.Cell("A1").Style.Font.FontColor =
                XLColor.White;

            instructions.Cell("A1").Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1F4FD7");

            instructions.Cell("A1").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            instructions.Cell("A3").Value = "Field";
            instructions.Cell("B3").Value = "Required";
            instructions.Cell("C3").Value = "Example";
            instructions.Cell("D3").Value = "Description";

            var instructionHeaderRange =
                instructions.Range("A3:D3");

            instructionHeaderRange.Style.Font.Bold = true;
            instructionHeaderRange.Style.Font.FontColor =
                XLColor.White;

            instructionHeaderRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#3346A8");

            var instructionRows = new[]
            {
                new[]
                {
                    "Invoice No",
                    "Yes",
                    "INV-2026-001",
                    "Enter a unique invoice number."
                },
                new[]
                {
                    "Date",
                    "Yes",
                    "15-07-2026",
                    "Enter the invoice date."
                },
                new[]
                {
                    "Bill To",
                    "No",
                    "WEG Industries (India) PVT LTD",
                    "Customer or billing company name."
                },
                new[]
                {
                    "PO/DC Date",
                    "No",
                    "02-05-2026",
                    "Purchase-order or delivery-challan date."
                },
                new[]
                {
                    "PO/DC NO",
                    "Yes",
                    "4519000001",
                    "PO number used for matching the material."
                },
                new[]
                {
                    "ITEM NO",
                    "Recommended",
                    "N-10",
                    "10, N10 and N-10 formats are supported."
                },
                new[]
                {
                    "MATERIAL",
                    "Yes",
                    "15464033",
                    "Material number used for matching."
                },
                new[]
                {
                    "DESCRIPTION",
                    "No",
                    "Split Clamp",
                    "Material description."
                },
                new[]
                {
                    "HSN/SAC",
                    "No",
                    "73269099",
                    "HSN or SAC code."
                },
                new[]
                {
                    "QTY",
                    "Yes",
                    "2",
                    "Invoice quantity must be greater than zero."
                },
                new[]
                {
                    "Price/Unit",
                    "No",
                    "1531.00",
                    "Invoice price or rate per unit."
                },
                new[]
                {
                    "GST",
                    "No",
                    "551.16",
                    "GST amount for the material row."
                },
                new[]
                {
                    "Amount",
                    "No",
                    "3062.00",
                    "Total value of the material row."
                }
            };

            for (var rowIndex = 0;
                 rowIndex < instructionRows.Length;
                 rowIndex++)
            {
                var excelRow =
                    rowIndex + 4;

                for (var columnIndex = 0;
                     columnIndex < instructionRows[rowIndex].Length;
                     columnIndex++)
                {
                    instructions.Cell(
                        excelRow,
                        columnIndex + 1).Value =
                            instructionRows[rowIndex][columnIndex];
                }
            }

            instructions.Columns(1, 4)
                .AdjustToContents();

            instructions.Column(1).Width = 22;
            instructions.Column(2).Width = 16;
            instructions.Column(3).Width = 28;
            instructions.Column(4).Width = 48;

            instructions.Column(4)
                .Style.Alignment.WrapText = true;

            var usedInstructionRange =
                instructions.RangeUsed();

            if (usedInstructionRange != null)
            {
                usedInstructionRange.Style.Border.OutsideBorder =
                    XLBorderStyleValues.Thin;

                usedInstructionRange.Style.Border.InsideBorder =
                    XLBorderStyleValues.Thin;

                usedInstructionRange.Style.Border.OutsideBorderColor =
                    XLColor.FromHtml("#D8DEE9");

                usedInstructionRange.Style.Border.InsideBorderColor =
                    XLColor.FromHtml("#E5E7EB");
            }

            worksheet.Position = 1;

            // ========================================================
            // RETURN EXCEL FILE
            // ========================================================

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "POTracker_Invoice_Import_Template.xlsx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1024L * 1024L * 200L)]
        [RequestFormLimits(
            ValueCountLimit = 10000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 1024L * 1024L * 200L)]
        public async Task<IActionResult> Upload(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Error = "Please select invoice PDF or Excel file.";
                return View();
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (extension != ".pdf" && extension != ".xlsx" && extension != ".xls")
            {
                ViewBag.Error = "Only PDF, XLSX and XLS invoice files are allowed.";
                return View();
            }

            var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "invoices");
            Directory.CreateDirectory(uploadFolder);

            var safeFileName = Path.GetFileName(file.FileName);
            var fileName = $"{Guid.NewGuid()}_{safeFileName}";
            var fullPath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            InvoiceImportPreviewViewModel model;

            try
            {
                if (extension == ".pdf")
                {
                    model = ExtractFromPdf(fullPath);
                }
                else
                {
                    model = ExtractFromExcel(fullPath);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Unable to read invoice file. Error: " + ex.Message;
                return View();
            }

            model.UploadedFileName = safeFileName ?? "";
            model.UploadedFilePath = "/uploads/invoices/" + fileName;

            if (IsInvalidInvoiceNumber(model.InvoiceNumber))
            {
                model.InvoiceNumber =
                    GetInvoiceNumberFromFileName(safeFileName);

                if (IsInvalidInvoiceNumber(model.InvoiceNumber))
                {
                    model.InvoiceNumber =
                        "REVIEW-" +
                        DateTime.Now.ToString("yyyyMMddHHmmss");
                }
            }

            if (model.Items == null || model.Items.Count == 0)
            {
                ViewBag.Error = "No invoice items found. Please check invoice format.";
                return View();
            }

            return View("Preview", model);
        }

        // ============================================================
        // UPLOAD AND EXTRACT MULTIPLE INVOICE FILES
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1024L * 1024L * 200L)]
        [RequestFormLimits(
            ValueCountLimit = 50000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 1024L * 1024L * 200L)]
        public async Task<IActionResult> UploadBatch(
            List<IFormFile>? invoiceFiles)
        {
            if (invoiceFiles == null || invoiceFiles.Count == 0)
            {
                ViewBag.Error =
                    "Please select one or more invoice files.";

                return View("Upload");
            }

            if (invoiceFiles.Count > MaxBatchFileCount)
            {
                ViewBag.Error =
                    $"You can upload a maximum of " +
                    $"{MaxBatchFileCount} invoice files at one time.";

                return View("Upload");
            }

            var totalSize = invoiceFiles.Sum(x => x?.Length ?? 0);

            if (totalSize > MaxBatchUploadSize)
            {
                ViewBag.Error =
                    "The combined upload size cannot exceed 200 MB.";

                return View("Upload");
            }

            var batchModel =
                new InvoiceBatchImportPreviewViewModel();

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "invoices");

            Directory.CreateDirectory(uploadFolder);

            foreach (var invoiceFile in invoiceFiles)
            {
                var fileResult =
                    new InvoiceBatchImportFileViewModel
                    {
                        OriginalFileName =
                            invoiceFile == null
                                ? string.Empty
                                : Path.GetFileName(invoiceFile.FileName),

                        FileSize = invoiceFile?.Length ?? 0,
                        IsSelected = false
                    };

                batchModel.Files.Add(fileResult);

                if (invoiceFile == null || invoiceFile.Length == 0)
                {
                    fileResult.ErrorMessage =
                        "The selected invoice file is empty.";

                    continue;
                }

                if (invoiceFile.Length > MaxInvoiceFileSize)
                {
                    fileResult.ErrorMessage =
                        "The invoice file exceeds the 20 MB limit.";

                    continue;
                }

                var extension = Path
                    .GetExtension(invoiceFile.FileName)
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
                    Path.GetFileName(invoiceFile.FileName);

                var storedFileName =
                    $"{Guid.NewGuid():N}_{safeFileName}";

                var physicalPath = Path.Combine(
                    uploadFolder,
                    storedFileName);

                try
                {
                    await using (var stream = new FileStream(
                                     physicalPath,
                                     FileMode.CreateNew))
                    {
                        await invoiceFile.CopyToAsync(stream);
                    }

                    var invoiceModel =
                        extension == ".pdf"
                            ? ExtractFromPdf(physicalPath)
                            : ExtractFromExcel(physicalPath);

                    invoiceModel.UploadedFileName = safeFileName;
                    invoiceModel.UploadedFilePath =
                        "/uploads/invoices/" + storedFileName;

                    if (IsInvalidInvoiceNumber(
                            invoiceModel.InvoiceNumber))
                    {
                        invoiceModel.InvoiceNumber =
                            GetInvoiceNumberFromFileName(safeFileName);

                        if (IsInvalidInvoiceNumber(
                                invoiceModel.InvoiceNumber))
                        {
                            invoiceModel.InvoiceNumber =
                                "REVIEW-" +
                                Guid.NewGuid().ToString("N")[..12]
                                    .ToUpperInvariant();
                        }
                    }

                    if (invoiceModel.Items == null ||
                        invoiceModel.Items.Count == 0)
                    {
                        if (System.IO.File.Exists(physicalPath))
                        {
                            System.IO.File.Delete(physicalPath);
                        }

                        fileResult.ErrorMessage =
                            "No invoice items were detected in this file.";

                        continue;
                    }

                    fileResult.Invoice = invoiceModel;
                    fileResult.IsSuccessful = true;
                    fileResult.IsSelected = true;
                }
                catch (Exception ex)
                {
                    if (System.IO.File.Exists(physicalPath))
                    {
                        System.IO.File.Delete(physicalPath);
                    }

                    fileResult.ErrorMessage =
                        "Unable to read this invoice file. " +
                        ex.Message;
                }
            }

            // Check every extracted invoice number with one database query.
            // This avoids one AnyAsync query per uploaded file.
            var extractedInvoiceNumbers = batchModel.Files
                .Where(x =>
                    x.IsSuccessful &&
                    x.Invoice != null &&
                    !string.IsNullOrWhiteSpace(
                        x.Invoice.InvoiceNumber))
                .Select(x =>
                    x.Invoice!.InvoiceNumber.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingInvoiceNumbers =
                extractedInvoiceNumbers.Count == 0
                    ? new List<string>()
                    : await _context.Invoices
                        .AsNoTracking()
                        .Where(x =>
                            extractedInvoiceNumbers.Contains(
                                x.InvoiceNumber))
                        .Select(x => x.InvoiceNumber)
                        .ToListAsync();

            var existingInvoiceNumberSet =
                existingInvoiceNumbers.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var fileResult in batchModel.Files.Where(x =>
                         x.IsSuccessful &&
                         x.Invoice != null))
            {
                var invoiceNumber =
                    fileResult.Invoice!.InvoiceNumber.Trim();

                fileResult.ExistsInDatabase =
                    existingInvoiceNumberSet.Contains(invoiceNumber);

                fileResult.IsSelected =
                    !fileResult.ExistsInDatabase;
            }

            var duplicateGroups = batchModel.Files
                .Where(x =>
                    x.IsSuccessful &&
                    x.Invoice != null &&
                    !string.IsNullOrWhiteSpace(
                        x.Invoice.InvoiceNumber))
                .GroupBy(
                    x => x.Invoice!.InvoiceNumber.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .ToList();

            foreach (var duplicateGroup in duplicateGroups)
            {
                foreach (var duplicateFile in duplicateGroup.Skip(1))
                {
                    duplicateFile.IsDuplicateInBatch = true;
                    duplicateFile.IsSelected = false;
                    duplicateFile.ErrorMessage =
                        "The same invoice number is already present " +
                        "in another selected file.";
                }
            }

            return View("BatchPreview", batchModel);
        }

        // ============================================================
        // SAVE MULTIPLE EXTRACTED INVOICES
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1024L * 1024L * 200L)]
        [RequestFormLimits(
            ValueCountLimit = 500000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 1024L * 1024L * 200L)]
        public async Task<IActionResult> SaveBatch(
            InvoiceBatchImportPreviewViewModel model)
        {
            if (model.Files == null || model.Files.Count == 0)
            {
                TempData["Error"] =
                    "No bulk invoice files were available to save.";

                return RedirectToAction(nameof(Upload));
            }

            var selectedFiles = model.Files
                .Where(x =>
                    x.IsSelected &&
                    x.IsSuccessful &&
                    !x.IsDuplicateInBatch &&
                    !x.ExistsInDatabase &&
                    x.Invoice != null)
                .ToList();

            if (selectedFiles.Count == 0)
            {
                TempData["Error"] =
                    "Please select at least one valid invoice file.";

                return RedirectToAction(nameof(Upload));
            }

            if (selectedFiles.Count > MaxBatchFileCount)
            {
                TempData["Error"] =
                    $"A maximum of {MaxBatchFileCount} invoices " +
                    "can be saved in one batch.";

                return RedirectToAction(nameof(Upload));
            }

            var selectedInvoiceNumbers = selectedFiles
                .Select(x =>
                    x.Invoice!.InvoiceNumber?.Trim() ?? string.Empty)
                .Where(x =>
                    !IsInvalidInvoiceNumber(x) &&
                    !x.StartsWith(
                        "REVIEW-",
                        StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingInvoiceNumbers =
                selectedInvoiceNumbers.Count == 0
                    ? new List<string>()
                    : await _context.Invoices
                        .AsNoTracking()
                        .Where(x =>
                            selectedInvoiceNumbers.Contains(
                                x.InvoiceNumber))
                        .Select(x => x.InvoiceNumber)
                        .ToListAsync();

            var existingInvoiceNumberSet =
                existingInvoiceNumbers.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            var resultModel =
                new InvoiceBatchImportResultViewModel
                {
                    BatchId = model.BatchId
                };

            var processedInvoiceNumbers =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var file in selectedFiles)
            {
                var invoiceModel = file.Invoice!;

                var itemResult =
                    new InvoiceBatchImportResultItemViewModel
                    {
                        OriginalFileName = file.OriginalFileName,
                        InvoiceNumber = invoiceModel.InvoiceNumber
                    };

                resultModel.Results.Add(itemResult);

                var invoiceNumber =
                    invoiceModel.InvoiceNumber?.Trim() ?? string.Empty;

                if (IsInvalidInvoiceNumber(invoiceNumber) ||
                    invoiceNumber.StartsWith(
                        "REVIEW-",
                        StringComparison.OrdinalIgnoreCase))
                {
                    itemResult.Message =
                        "A valid invoice number is required.";

                    continue;
                }

                if (!processedInvoiceNumbers.Add(invoiceNumber))
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

                try
                {
                    if (existingInvoiceNumberSet.Contains(
                            invoiceNumber))
                    {
                        itemResult.Message =
                            "This invoice number already exists in the database.";

                        continue;
                    }

                    var saveActionResult =
                        await SaveAll(invoiceModel);

                    if (saveActionResult is RedirectToActionResult redirect &&
                        string.Equals(
                            redirect.ActionName,
                            nameof(History),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        var latestImport =
                            await _context.VyaparInvoiceImportBatches
                                .Where(x =>
                                    x.FileName ==
                                    invoiceModel.UploadedFileName)
                                .OrderByDescending(x => x.Id)
                                .FirstOrDefaultAsync();

                        itemResult.IsImported = true;
                        itemResult.RowsSaved =
                            latestImport?.SuccessRows ?? 0;

                        var pendingRows =
                            Math.Max(
                                0,
                                invoiceModel.Items.Count -
                                itemResult.RowsSaved);

                        itemResult.Message =
                            $"Imported successfully. " +
                            $"Success: {itemResult.RowsSaved}, " +
                            $"Pending: {pendingRows}.";
                    }
                    else
                    {
                        itemResult.Message =
                            TempData["Error"]?.ToString() ??
                            ViewData["Error"]?.ToString() ??
                            "The invoice could not be saved.";
                    }
                }
                catch (Exception ex)
                {
                    itemResult.Message =
                        "Invoice save failed. " + ex.Message;
                }
                finally
                {
                    TempData.Remove("Success");
                    TempData.Remove("Error");
                    ViewData.Remove("Error");
                }
            }

            return View("BatchResult", resultModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1024L * 1024L * 200L)]
        [RequestFormLimits(
            ValueCountLimit = 10000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 1024L * 1024L * 200L)]
        public async Task<IActionResult> SaveAll(
            InvoiceImportPreviewViewModel model)
        {
            var items =
                model.Items;

            if (items == null ||
                items.Count == 0)
            {
                TempData["Error"] =
                    "No invoice items found to save.";

                return RedirectToAction(
                    nameof(Upload));
            }

            var invoiceNumber =
                model.InvoiceNumber
                    ?.Trim() ?? "";

            var uploadedFileName =
                model.UploadedFileName
                    ?.Trim() ?? "";

            if (IsInvalidInvoiceNumber(
                    invoiceNumber) ||
                invoiceNumber.StartsWith(
                    "REVIEW-",
                    StringComparison.OrdinalIgnoreCase))
            {
                ViewData["Error"] =
                    "Enter the correct invoice number before saving.";

                return View(
                    "Preview",
                    model);
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
                    else
                    {
                        var alreadyImported =
                            existingSuccessRows.Any(x =>
                                NormalizeKey(
                                    x.PONumber) ==
                                    poKey &&
                                NormalizeKey(
                                    x.MaterialNumber) ==
                                    materialKey &&
                                NormalizeLineItem(
                                    x.LineItem) ==
                                    lineItem);

                        if (alreadyImported)
                        {
                            pendingReason =
                                "This invoice row was already imported successfully. Stored as Pending for review.";

                            storePendingItem =
                                false;
                        }
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

                    var invoiceItemAlreadyExists =
                        await _context.InvoiceItems
                            .AnyAsync(x =>
                                x.InvoiceId ==
                                    invoice.Id &&
                                x.MaterialId ==
                                    material.Id &&
                                x.LineItem ==
                                    resolvedLineItem);

                    if (invoiceItemAlreadyExists)
                    {
                        pendingReason =
                            "This invoice item is already saved. Stored as Pending for review.";

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

                TempData["Success"] =
                    $"Invoice import completed. " +
                    $"Total: {items.Count}, " +
                    $"Success: {successRows}, " +
                    $"Pending: {pendingRows}.";

                return RedirectToAction(
                    nameof(History));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Invoice import failed and no partial changes " +
                    "were saved. Error: " +
                    ex.Message;

                return RedirectToAction(
                    nameof(Upload));
            }
        }

        private void AddPendingInvoiceItem(
            Invoice invoice,
            InvoiceImportPreviewViewModel model,
            InvoiceImportItemViewModel item,
            string poNumber,
            string lineItem,
            string materialNo,
            string reason,
            HashSet<string> currentPendingKeys)
        {
            var pendingKey =
                BuildInvoiceRowKey(
                    poNumber,
                    materialNo,
                    lineItem,
                    item.InvoiceQty);

            if (!currentPendingKeys.Add(
                    pendingKey))
            {
                return;
            }

            _context.PendingInvoiceItems.Add(
                new PendingInvoiceItem
                {
                    InvoiceId =
                        invoice.Id,

                    InvoiceNumber =
                        invoice.InvoiceNumber,

                    InvoiceDate =
                        model.InvoiceDate,

                    CustomerName =
                        model.CustomerName,

                    PONumber =
                        poNumber,

                    LineItem =
                        lineItem,

                    MaterialNo =
                        materialNo,

                    Description =
                        item.Description,

                    HsnSacCode =
                        item.HsnSacCode,

                    InvoiceQty =
                        item.InvoiceQty,

                    PriceUnit =
                        item.PriceUnit,

                    GstAmount =
                        item.GstAmount,

                    Amount =
                        item.Amount,

                    Status =
                        "Pending",

                    ErrorMessage =
                        reason,

                    CreatedDate =
                        DateTime.Now
                });
        }

        private static string BuildInvoiceRowKey(
            string? poNumber,
            string? materialNumber,
            string? lineItem,
            decimal invoiceQty)
        {
            return
                $"{NormalizeKey(poNumber)}|" +
                $"{NormalizeKey(materialNumber)}|" +
                $"{NormalizeLineItem(lineItem)}|" +
                invoiceQty.ToString(
                    CultureInfo.InvariantCulture);
        }

        [HttpGet]
        public async Task<IActionResult> InvoiceList(
            int page = 1,
            int pageSize = 50)
        {
            page = Math.Max(page, 1);

            var allowedPageSizes =
                new[] { 10, 25, 50, 100, 200 };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 50;
            }

            var totalInvoices =
                await _context.Invoices
                    .AsNoTracking()
                    .CountAsync();

            var itemSummary =
                await _context.InvoiceItems
                    .AsNoTracking()
                    .GroupBy(x => 1)
                    .Select(group => new
                    {
                        TotalItems = group.Count(),
                        TotalInvoiceQty = group.Sum(x => x.InvoiceQty),
                        TotalGst = group.Sum(x => x.GstAmount),
                        TotalAmount = group.Sum(x => x.Amount)
                    })
                    .FirstOrDefaultAsync();

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalInvoices / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var invoices = await _context.Invoices
                .AsNoTracking()
                .Include(x => x.Items)
                .AsSplitQuery()
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalInvoices = totalInvoices;
            ViewBag.TotalItems = itemSummary?.TotalItems ?? 0;
            ViewBag.TotalInvoiceQty = itemSummary?.TotalInvoiceQty ?? 0m;
            ViewBag.TotalGst = itemSummary?.TotalGst ?? 0m;
            ViewBag.TotalAmount = itemSummary?.TotalAmount ?? 0m;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            return View(invoices);
        }


        // ============================================================
        // EXPORT ALL INVOICES INTO ONE CONSOLIDATED EXCEL
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> ExportAllInvoicesExcel()
        {
            var invoices = await _context.Invoices
                .AsNoTracking()
                .OrderByDescending(x => x.InvoiceDate)
                .ThenByDescending(x => x.CreatedDate)
                .ToListAsync();

            if (invoices.Count == 0)
            {
                TempData["Error"] =
                    "No invoices are available to export.";

                return RedirectToAction(nameof(InvoiceList));
            }

            var invoiceItems = await _context.InvoiceItems
                .AsNoTracking()
                .OrderBy(x => x.InvoiceId)
                .ThenBy(x => x.PONumber)
                .ThenBy(x => x.LineItem)
                .ThenBy(x => x.MaterialNo)
                .ToListAsync();

            var pendingItems =
                await _context.PendingInvoiceItems
                    .AsNoTracking()
                    .Where(x =>
                        x.Status == "Pending")
                    .OrderBy(x =>
                        x.InvoiceId)
                    .ThenBy(x =>
                        x.PONumber)
                    .ThenBy(x =>
                        x.LineItem)
                    .ThenBy(x =>
                        x.MaterialNo)
                    .ToListAsync();

            var materialIds = invoiceItems
                .Select(x => x.MaterialId)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var materials = materialIds.Count == 0
                ? new List<Material>()
                : await _context.Materials
                    .AsNoTracking()
                    .Where(x => materialIds.Contains(x.Id))
                    .ToListAsync();

            var invoiceMap = invoices
                .ToDictionary(x => x.Id);

            var materialMap = materials
                .ToDictionary(x => x.Id);

            var matchedByInvoice = invoiceItems
                .GroupBy(x => x.InvoiceId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var pendingByInvoice = pendingItems
                .GroupBy(x => x.InvoiceId)
                .ToDictionary(x => x.Key, x => x.ToList());

            using var workbook = new XLWorkbook();

            // ========================================================
            // SHEET 1: ALL INVOICE MATERIAL ROWS
            // ========================================================

            var mergedSheet =
                workbook.Worksheets.Add("Merged Invoice");

            var mergedHeaders = new[]
            {
                "Sl.No",
                "Invoice Number",
                "Invoice Date",
                "Customer Name",
                "PO Number",
                "Line Item",
                "Material Number",
                "Description",
                "HSN/SAC",
                "Invoice Qty",
                "Price/Unit",
                "GST Amount",
                "Amount",
                "Record Status",
                "PO Qty",
                "Total Invoiced Qty",
                "Invoice Balance Qty",
                "Dispatch Qty",
                "Dispatch Pending Qty",
                "Invoice Status",
                "Dispatch Status",
                "Delivery Date",
                "Uploaded File",
                "Imported Date",
                "Notes"
            };

            for (var columnIndex = 0;
                 columnIndex < mergedHeaders.Length;
                 columnIndex++)
            {
                mergedSheet.Cell(
                    1,
                    columnIndex + 1).Value =
                        mergedHeaders[columnIndex];
            }

            var mergedHeaderRange = mergedSheet.Range(
                1,
                1,
                1,
                mergedHeaders.Length);

            mergedHeaderRange.Style.Font.Bold = true;
            mergedHeaderRange.Style.Font.FontColor = XLColor.White;
            mergedHeaderRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1F4FD7");
            mergedHeaderRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            mergedHeaderRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;
            mergedHeaderRange.Style.Alignment.WrapText = true;

            mergedSheet.Row(1).Height = 36;

            var mergedRow = 2;
            var serialNumber = 1;

            foreach (var item in invoiceItems)
            {
                invoiceMap.TryGetValue(
                    item.InvoiceId,
                    out var invoice);

                materialMap.TryGetValue(
                    item.MaterialId,
                    out var material);

                mergedSheet.Cell(mergedRow, 1).Value =
                    serialNumber++;

                mergedSheet.Cell(mergedRow, 2).Value =
                    invoice?.InvoiceNumber ?? "";

                if (invoice?.InvoiceDate != null)
                {
                    mergedSheet.Cell(mergedRow, 3).Value =
                        invoice.InvoiceDate.Value;

                    mergedSheet.Cell(mergedRow, 3)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy";
                }

                mergedSheet.Cell(mergedRow, 4).Value =
                    invoice?.CustomerName ?? "";

                mergedSheet.Cell(mergedRow, 5).Value =
                    item.PONumber ?? "";

                mergedSheet.Cell(mergedRow, 6).Value =
                    item.LineItem ?? "";

                mergedSheet.Cell(mergedRow, 7).Value =
                    item.MaterialNo ?? "";

                mergedSheet.Cell(mergedRow, 8).Value =
                    item.Description ?? "";

                mergedSheet.Cell(mergedRow, 9).Value =
                    item.HsnSacCode ?? "";

                mergedSheet.Cell(mergedRow, 10).Value =
                    item.InvoiceQty;

                mergedSheet.Cell(mergedRow, 11).Value =
                    item.PriceUnit;

                mergedSheet.Cell(mergedRow, 12).Value =
                    item.GstAmount;

                mergedSheet.Cell(mergedRow, 13).Value =
                    item.Amount;

                mergedSheet.Cell(mergedRow, 14).Value =
                    "Matched";

                mergedSheet.Cell(mergedRow, 15).Value =
                    material?.Qty ?? 0;

                mergedSheet.Cell(mergedRow, 16).Value =
                    material?.InvoiceQty ?? 0;

                mergedSheet.Cell(mergedRow, 17).Value =
                    material?.BalanceQty ?? 0;

                mergedSheet.Cell(mergedRow, 18).Value =
                    material?.DispatchQty ?? 0;

                mergedSheet.Cell(mergedRow, 19).Value =
                    material?.PendingQty ?? 0;

                mergedSheet.Cell(mergedRow, 20).Value =
                    material?.InvoiceStatus ?? "";

                mergedSheet.Cell(mergedRow, 21).Value =
                    material?.DispatchStatus ?? "";

                if (material?.DeliveryDate != null)
                {
                    mergedSheet.Cell(mergedRow, 22).Value =
                        material.DeliveryDate.Value;

                    mergedSheet.Cell(mergedRow, 22)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy";
                }

                mergedSheet.Cell(mergedRow, 23).Value =
                    invoice?.UploadedFileName ?? "";

                if (invoice != null)
                {
                    mergedSheet.Cell(mergedRow, 24).Value =
                        invoice.CreatedDate;

                    mergedSheet.Cell(mergedRow, 24)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy HH:mm";
                }

                mergedSheet.Cell(mergedRow, 25).Value =
                    "PO and material matched successfully.";

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#DCFCE7");

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Font.FontColor =
                        XLColor.FromHtml("#166534");

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Font.Bold = true;

                mergedRow++;
            }

            foreach (var item in pendingItems)
            {
                invoiceMap.TryGetValue(
                    item.InvoiceId,
                    out var invoice);

                mergedSheet.Cell(mergedRow, 1).Value =
                    serialNumber++;

                mergedSheet.Cell(mergedRow, 2).Value =
                    !string.IsNullOrWhiteSpace(item.InvoiceNumber)
                        ? item.InvoiceNumber
                        : invoice?.InvoiceNumber ?? "";

                var invoiceDate =
                    item.InvoiceDate ??
                    invoice?.InvoiceDate;

                if (invoiceDate.HasValue)
                {
                    mergedSheet.Cell(mergedRow, 3).Value =
                        invoiceDate.Value;

                    mergedSheet.Cell(mergedRow, 3)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy";
                }

                mergedSheet.Cell(mergedRow, 4).Value =
                    !string.IsNullOrWhiteSpace(item.CustomerName)
                        ? item.CustomerName
                        : invoice?.CustomerName ?? "";

                mergedSheet.Cell(mergedRow, 5).Value =
                    item.PONumber ?? "";

                mergedSheet.Cell(mergedRow, 6).Value =
                    item.LineItem ?? "";

                mergedSheet.Cell(mergedRow, 7).Value =
                    item.MaterialNo ?? "";

                mergedSheet.Cell(mergedRow, 8).Value =
                    item.Description ?? "";

                mergedSheet.Cell(mergedRow, 9).Value =
                    item.HsnSacCode ?? "";

                mergedSheet.Cell(mergedRow, 10).Value =
                    item.InvoiceQty;

                mergedSheet.Cell(mergedRow, 11).Value =
                    item.PriceUnit;

                mergedSheet.Cell(mergedRow, 12).Value =
                    item.GstAmount;

                mergedSheet.Cell(mergedRow, 13).Value =
                    item.Amount;

                mergedSheet.Cell(mergedRow, 14).Value =
                    item.Status ?? "Pending";

                mergedSheet.Cell(mergedRow, 23).Value =
                    invoice?.UploadedFileName ?? "";

                mergedSheet.Cell(mergedRow, 24).Value =
                    item.CreatedDate;

                mergedSheet.Cell(mergedRow, 24)
                    .Style.DateFormat.Format =
                        "dd-MMM-yyyy HH:mm";

                mergedSheet.Cell(mergedRow, 25).Value =
                    item.ErrorMessage ??
                    "PO or material is not matched.";

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#FEF3C7");

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Font.FontColor =
                        XLColor.FromHtml("#92400E");

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Font.Bold = true;

                mergedRow++;
            }

            if (mergedRow == 2)
            {
                mergedSheet.Cell(2, 1).Value =
                    "No invoice material rows found.";
            }
            else
            {
                mergedSheet.Range(
                        2,
                        10,
                        mergedRow - 1,
                        19)
                    .Style.NumberFormat.Format =
                        "#,##0.00";
            }

            mergedSheet.SheetView.FreezeRows(1);

            if (mergedRow > 2)
            {
                mergedSheet.Range(
                        1,
                        1,
                        mergedRow - 1,
                        mergedHeaders.Length)
                    .SetAutoFilter();
            }

            mergedSheet.Column(1).Width = 8;
            mergedSheet.Column(2).Width = 22;
            mergedSheet.Column(3).Width = 16;
            mergedSheet.Column(4).Width = 30;
            mergedSheet.Column(5).Width = 18;
            mergedSheet.Column(6).Width = 13;
            mergedSheet.Column(7).Width = 18;
            mergedSheet.Column(8).Width = 40;
            mergedSheet.Column(9).Width = 15;
            mergedSheet.Columns(10, 19).Width = 17;
            mergedSheet.Columns(20, 21).Width = 20;
            mergedSheet.Column(22).Width = 16;
            mergedSheet.Column(23).Width = 30;
            mergedSheet.Column(24).Width = 21;
            mergedSheet.Column(25).Width = 55;

            mergedSheet.Column(8)
                .Style.Alignment.WrapText = true;

            mergedSheet.Column(25)
                .Style.Alignment.WrapText = true;

            // ========================================================
            // SHEET 2: INVOICE-WISE SUMMARY
            // ========================================================

            var summarySheet =
                workbook.Worksheets.Add("Invoice Summary");

            var summaryHeaders = new[]
            {
                "Sl.No",
                "Invoice Number",
                "Invoice Date",
                "Customer Name",
                "Matched Rows",
                "Pending Rows",
                "Total Rows",
                "Matched Qty",
                "Pending Qty",
                "Total Qty",
                "Matched GST",
                "Pending GST",
                "Total GST",
                "Matched Amount",
                "Pending Amount",
                "Total Amount",
                "Uploaded File",
                "Imported Date"
            };

            for (var columnIndex = 0;
                 columnIndex < summaryHeaders.Length;
                 columnIndex++)
            {
                summarySheet.Cell(
                    1,
                    columnIndex + 1).Value =
                        summaryHeaders[columnIndex];
            }

            var summaryHeaderRange = summarySheet.Range(
                1,
                1,
                1,
                summaryHeaders.Length);

            summaryHeaderRange.Style.Font.Bold = true;
            summaryHeaderRange.Style.Font.FontColor = XLColor.White;
            summaryHeaderRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#334155");
            summaryHeaderRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            summaryHeaderRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;
            summaryHeaderRange.Style.Alignment.WrapText = true;

            summarySheet.Row(1).Height = 34;

            var summaryRow = 2;

            for (var index = 0;
                 index < invoices.Count;
                 index++)
            {
                var invoice =
                    invoices[index];

                var matchedRows =
                    matchedByInvoice.TryGetValue(
                        invoice.Id,
                        out var matchedList)
                        ? matchedList
                        : new List<InvoiceItem>();

                var storedPendingRows =
                    pendingByInvoice.TryGetValue(
                        invoice.Id,
                        out var pendingList)
                        ? pendingList
                        : new List<PendingInvoiceItem>();

                var matchedQty =
                    matchedRows.Sum(x => x.InvoiceQty);

                var pendingQty =
                    storedPendingRows.Sum(x => x.InvoiceQty);

                var matchedGst =
                    matchedRows.Sum(x => x.GstAmount);

                var pendingGst =
                    storedPendingRows.Sum(x => x.GstAmount);

                var matchedAmount =
                    matchedRows.Sum(x => x.Amount);

                var pendingAmount =
                    storedPendingRows.Sum(x => x.Amount);

                summarySheet.Cell(summaryRow, 1).Value =
                    index + 1;

                summarySheet.Cell(summaryRow, 2).Value =
                    invoice.InvoiceNumber;

                if (invoice.InvoiceDate.HasValue)
                {
                    summarySheet.Cell(summaryRow, 3).Value =
                        invoice.InvoiceDate.Value;

                    summarySheet.Cell(summaryRow, 3)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy";
                }

                summarySheet.Cell(summaryRow, 4).Value =
                    invoice.CustomerName ?? "";

                summarySheet.Cell(summaryRow, 5).Value =
                    matchedRows.Count;

                summarySheet.Cell(summaryRow, 6).Value =
                    storedPendingRows.Count;

                summarySheet.Cell(summaryRow, 7).Value =
                    matchedRows.Count +
                    storedPendingRows.Count;

                summarySheet.Cell(summaryRow, 8).Value =
                    matchedQty;

                summarySheet.Cell(summaryRow, 9).Value =
                    pendingQty;

                summarySheet.Cell(summaryRow, 10).Value =
                    matchedQty + pendingQty;

                summarySheet.Cell(summaryRow, 11).Value =
                    matchedGst;

                summarySheet.Cell(summaryRow, 12).Value =
                    pendingGst;

                summarySheet.Cell(summaryRow, 13).Value =
                    matchedGst + pendingGst;

                summarySheet.Cell(summaryRow, 14).Value =
                    matchedAmount;

                summarySheet.Cell(summaryRow, 15).Value =
                    pendingAmount;

                summarySheet.Cell(summaryRow, 16).Value =
                    matchedAmount + pendingAmount;

                summarySheet.Cell(summaryRow, 17).Value =
                    invoice.UploadedFileName ?? "";

                summarySheet.Cell(summaryRow, 18).Value =
                    invoice.CreatedDate;

                summarySheet.Cell(summaryRow, 18)
                    .Style.DateFormat.Format =
                        "dd-MMM-yyyy HH:mm";

                summaryRow++;
            }

            if (summaryRow > 2)
            {
                summarySheet.Range(
                        2,
                        8,
                        summaryRow - 1,
                        16)
                    .Style.NumberFormat.Format =
                        "#,##0.00";
            }

            var totalRow = summaryRow;

            summarySheet.Cell(totalRow, 1).Value =
                "TOTAL";

            summarySheet.Range(
                    totalRow,
                    1,
                    totalRow,
                    4)
                .Merge();

            summarySheet.Cell(totalRow, 5).Value =
                invoiceItems.Count;

            summarySheet.Cell(totalRow, 6).Value =
                pendingItems.Count;

            summarySheet.Cell(totalRow, 7).Value =
                invoiceItems.Count +
                pendingItems.Count;

            summarySheet.Cell(totalRow, 8).Value =
                invoiceItems.Sum(x => x.InvoiceQty);

            summarySheet.Cell(totalRow, 9).Value =
                pendingItems.Sum(x => x.InvoiceQty);

            summarySheet.Cell(totalRow, 10).Value =
                invoiceItems.Sum(x => x.InvoiceQty) +
                pendingItems.Sum(x => x.InvoiceQty);

            summarySheet.Cell(totalRow, 11).Value =
                invoiceItems.Sum(x => x.GstAmount);

            summarySheet.Cell(totalRow, 12).Value =
                pendingItems.Sum(x => x.GstAmount);

            summarySheet.Cell(totalRow, 13).Value =
                invoiceItems.Sum(x => x.GstAmount) +
                pendingItems.Sum(x => x.GstAmount);

            summarySheet.Cell(totalRow, 14).Value =
                invoiceItems.Sum(x => x.Amount);

            summarySheet.Cell(totalRow, 15).Value =
                pendingItems.Sum(x => x.Amount);

            summarySheet.Cell(totalRow, 16).Value =
                invoiceItems.Sum(x => x.Amount) +
                pendingItems.Sum(x => x.Amount);

            summarySheet.Range(
                    totalRow,
                    1,
                    totalRow,
                    summaryHeaders.Length)
                .Style.Font.Bold = true;

            summarySheet.Range(
                    totalRow,
                    1,
                    totalRow,
                    summaryHeaders.Length)
                .Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#E8EEFF");

            summarySheet.Range(
                    totalRow,
                    1,
                    totalRow,
                    summaryHeaders.Length)
                .Style.Font.FontColor =
                    XLColor.FromHtml("#1F3A8A");

            summarySheet.Range(
                    totalRow,
                    8,
                    totalRow,
                    16)
                .Style.NumberFormat.Format =
                    "#,##0.00";

            summarySheet.SheetView.FreezeRows(1);

            if (invoices.Count > 0)
            {
                summarySheet.Range(
                        1,
                        1,
                        totalRow - 1,
                        summaryHeaders.Length)
                    .SetAutoFilter();
            }

            summarySheet.Column(1).Width = 9;
            summarySheet.Column(2).Width = 22;
            summarySheet.Column(3).Width = 16;
            summarySheet.Column(4).Width = 30;
            summarySheet.Columns(5, 16).Width = 16;
            summarySheet.Column(17).Width = 30;
            summarySheet.Column(18).Width = 21;

            // ========================================================
            // COMMON WORKBOOK STYLING
            // ========================================================

            foreach (var worksheet in workbook.Worksheets)
            {
                var usedRange =
                    worksheet.RangeUsed();

                if (usedRange != null)
                {
                    usedRange.Style.Border.OutsideBorder =
                        XLBorderStyleValues.Thin;

                    usedRange.Style.Border.InsideBorder =
                        XLBorderStyleValues.Hair;

                    usedRange.Style.Border.OutsideBorderColor =
                        XLColor.FromHtml("#CBD5E1");

                    usedRange.Style.Border.InsideBorderColor =
                        XLColor.FromHtml("#E5E7EB");

                    usedRange.Style.Alignment.Vertical =
                        XLAlignmentVerticalValues.Top;
                }
            }

            mergedSheet.Position = 1;
            summarySheet.Position = 2;

            using var outputStream =
                new MemoryStream();

            workbook.SaveAs(outputStream);

            var downloadFileName =
                "Merged_All_Invoices_" +
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss") +
                ".xlsx";

            return File(
                outputStream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                downloadFileName);
        }

        // ============================================================
        // OLD LINK COMPATIBILITY
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> ExportInvoiceExcel()
        {
            // The old row button may still exist in the browser cache.
            // It now downloads the same consolidated Excel file.
            return await ExportAllInvoicesExcel();
        }

        // ============================================================
        // DELETE ONE INVOICE
        // Restores material invoice and dispatch quantities.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteInvoice(
            int invoiceId)
        {
            if (invoiceId <= 0)
            {
                TempData["Error"] =
                    "Invalid invoice.";

                return RedirectToAction(
                    nameof(InvoiceList));
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var invoice =
                    await _context.Invoices
                        .FirstOrDefaultAsync(x =>
                            x.Id == invoiceId);

                if (invoice == null)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Invoice not found.";

                    return RedirectToAction(
                        nameof(InvoiceList));
                }

                var invoiceNumber =
                    string.IsNullOrWhiteSpace(
                        invoice.InvoiceNumber)
                        ? $"Invoice #{invoice.Id}"
                        : invoice.InvoiceNumber;

                await DeleteInvoiceDataAsync(
                    invoice);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    $"Invoice {invoiceNumber} deleted successfully " +
                    "and material invoice/dispatch quantities were restored.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to delete the invoice. " +
                    "No partial changes were saved. Error: " +
                    ex.Message;
            }

            return RedirectToAction(
                nameof(InvoiceList));
        }

        // ============================================================
        // DELETE MULTIPLE INVOICES
        // All selected invoices are deleted in one transaction.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDeleteInvoices(
            List<int>? selectedIds,
            string? returnUrl)
        {
            var invoiceIds =
                selectedIds?
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ??
                new List<int>();

            if (invoiceIds.Count == 0)
            {
                TempData["Error"] =
                    "Select at least one invoice to delete.";

                return RedirectToInvoiceList(
                    returnUrl);
            }

            if (invoiceIds.Count > 200)
            {
                TempData["Error"] =
                    "A maximum of 200 invoices can be deleted " +
                    "at one time.";

                return RedirectToInvoiceList(
                    returnUrl);
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var invoices =
                    await _context.Invoices
                        .Where(x =>
                            invoiceIds.Contains(x.Id))
                        .OrderBy(x => x.Id)
                        .ToListAsync();

                var missingCount =
                    invoiceIds.Count -
                    invoices.Count;

                if (invoices.Count == 0)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "None of the selected invoices were found.";

                    return RedirectToInvoiceList(
                        returnUrl);
                }

                foreach (var invoice in invoices)
                {
                    await DeleteInvoiceDataAsync(
                        invoice);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    $"{invoices.Count} invoice(s) deleted successfully. " +
                    "Material invoice and dispatch quantities were restored.";

                if (missingCount > 0)
                {
                    TempData["Error"] =
                        $"{missingCount} selected invoice(s) were not found.";
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to delete the selected invoices. " +
                    "No partial changes were saved. Error: " +
                    ex.Message;
            }

            return RedirectToInvoiceList(
                returnUrl);
        }

        // ============================================================
        // SHARED INVOICE DELETE LOGIC
        // ============================================================

        private async Task DeleteInvoiceDataAsync(
            Invoice invoice)
        {
            var invoiceItems =
                await _context.InvoiceItems
                    .Where(x =>
                        x.InvoiceId == invoice.Id)
                    .OrderBy(x => x.Id)
                    .ToListAsync();

            var materialIds =
                invoiceItems
                    .Select(x => x.MaterialId)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

            var materials =
                materialIds.Count == 0
                    ? new List<Material>()
                    : await _context.Materials
                        .Where(x =>
                            materialIds.Contains(x.Id))
                        .ToListAsync();

            var materialMap =
                materials.ToDictionary(
                    x => x.Id);

            foreach (var invoiceItem in invoiceItems)
            {
                if (!materialMap.TryGetValue(
                        invoiceItem.MaterialId,
                        out var material))
                {
                    continue;
                }

                // Restore invoice quantity.
                material.InvoiceQty =
                    Math.Max(
                        0,
                        material.InvoiceQty -
                        invoiceItem.InvoiceQty);

                material.BalanceQty =
                    Math.Max(
                        0,
                        material.Qty -
                        material.InvoiceQty);

                material.InvoiceStatus =
                    GetInvoiceStatus(
                        material.Qty,
                        material.InvoiceQty,
                        material.BalanceQty);

                // In this project invoice quantity is also
                // reflected as dispatch quantity.
                material.DispatchQty =
                    Math.Max(
                        0,
                        material.DispatchQty -
                        invoiceItem.InvoiceQty);

                material.PendingQty =
                    Math.Max(
                        0,
                        material.Qty -
                        material.DispatchQty);

                if (material.DispatchQty <= 0)
                {
                    material.DispatchDate = null;
                }

                material.DispatchStatus =
                    GetDispatchStatus(
                        material.Qty,
                        material.DispatchQty,
                        material.PendingQty);
            }

            if (materials.Count > 0)
            {
                _context.Materials.UpdateRange(
                    materials);
            }

            var pendingItems =
                await _context.PendingInvoiceItems
                    .Where(x =>
                        x.InvoiceId == invoice.Id)
                    .ToListAsync();

            if (pendingItems.Count > 0)
            {
                _context.PendingInvoiceItems
                    .RemoveRange(pendingItems);
            }

            if (invoiceItems.Count > 0)
            {
                _context.InvoiceItems
                    .RemoveRange(invoiceItems);
            }

            if (!string.IsNullOrWhiteSpace(
                    invoice.InvoiceNumber))
            {
                var importRows =
                    await _context.VyaparInvoiceImportRows
                        .Where(x =>
                            x.InvoiceNumber ==
                            invoice.InvoiceNumber)
                        .ToListAsync();

                foreach (var row in importRows)
                {
                    row.Status =
                        "Deleted";

                    row.ErrorMessage =
                        "Invoice deleted. This row can be imported again.";
                }

                if (importRows.Count > 0)
                {
                    _context.VyaparInvoiceImportRows
                        .UpdateRange(importRows);
                }
            }

            _context.Invoices.Remove(
                invoice);
        }

        private IActionResult RedirectToInvoiceList(
            string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(
                    returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(
                    returnUrl);
            }

            return RedirectToAction(
                nameof(InvoiceList));
        }

        [HttpGet]
        public async Task<IActionResult> History(
            int page = 1,
            int pageSize = 25)
        {
            page = Math.Max(page, 1);

            var allowedPageSizes =
                new[] { 10, 25, 50, 100, 200 };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 25;
            }

            var totalBatches =
                await _context.VyaparInvoiceImportBatches
                    .AsNoTracking()
                    .CountAsync();

            var historySummary =
                await _context.VyaparInvoiceImportBatches
                    .AsNoTracking()
                    .Select(batch => new
                    {
                        TotalRows = batch.Rows.Count > 0
                            ? batch.Rows.Count
                            : batch.TotalRows,
                        SuccessRows = batch.Rows.Count(row =>
                            row.Status == "Success"),
                        PendingRows = batch.Rows.Count(row =>
                            row.Status == "Pending" ||
                            row.Status == "Failed"),
                        DeletedRows = batch.Rows.Count(row =>
                            row.Status == "Deleted")
                    })
                    .GroupBy(x => 1)
                    .Select(group => new
                    {
                        TotalRows = group.Sum(x => x.TotalRows),
                        SuccessRows = group.Sum(x => x.SuccessRows),
                        PendingRows = group.Sum(x => x.PendingRows),
                        DeletedRows = group.Sum(x => x.DeletedRows)
                    })
                    .FirstOrDefaultAsync();

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalBatches / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var batches =
                await _context
                    .VyaparInvoiceImportBatches
                    .AsNoTracking()
                    .Include(x => x.Rows)
                    .AsSplitQuery()
                    .OrderByDescending(x =>
                        x.ImportDate)
                    .ThenByDescending(x => x.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

            ViewBag.TotalBatches = totalBatches;
            ViewBag.TotalRows = historySummary?.TotalRows ?? 0;
            ViewBag.SuccessRows = historySummary?.SuccessRows ?? 0;
            ViewBag.PendingRows = historySummary?.PendingRows ?? 0;
            ViewBag.DeletedRows = historySummary?.DeletedRows ?? 0;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            return View(batches);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHistory(int id)
        {
            if (id <= 0)
            {
                TempData["Error"] =
                    "Invalid invoice history record.";

                return RedirectToAction(nameof(History));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var batch = await _context.VyaparInvoiceImportBatches
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (batch == null)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Invoice history record was not found.";

                    return RedirectToAction(nameof(History));
                }

                var historyRows = await _context.VyaparInvoiceImportRows
                    .Where(x => x.BatchId == id)
                    .ToListAsync();

                if (historyRows.Count > 0)
                {
                    _context.VyaparInvoiceImportRows
                        .RemoveRange(historyRows);
                }

                _context.VyaparInvoiceImportBatches
                    .Remove(batch);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    $"Invoice import history '{batch.FileName}' " +
                    "was deleted successfully.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to delete the invoice history record. " +
                    "Please try again.";
            }

            return RedirectToAction(nameof(History));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelectedHistory(
            List<int>? selectedIds)
        {
            var validIds = (selectedIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (validIds.Count == 0)
            {
                TempData["Error"] =
                    "Please select at least one invoice history record.";

                return RedirectToAction(nameof(History));
            }

            if (validIds.Count > 200)
            {
                TempData["Error"] =
                    "A maximum of 200 invoice history records " +
                    "can be deleted at one time.";

                return RedirectToAction(nameof(History));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var batches =
                    await _context.VyaparInvoiceImportBatches
                        .Where(x => validIds.Contains(x.Id))
                        .ToListAsync();

                if (batches.Count == 0)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "The selected invoice history records " +
                        "were not found.";

                    return RedirectToAction(nameof(History));
                }

                var foundIds = batches
                    .Select(x => x.Id)
                    .ToList();

                var historyRows =
                    await _context.VyaparInvoiceImportRows
                        .Where(x => foundIds.Contains(x.BatchId))
                        .ToListAsync();

                if (historyRows.Count > 0)
                {
                    _context.VyaparInvoiceImportRows
                        .RemoveRange(historyRows);
                }

                _context.VyaparInvoiceImportBatches
                    .RemoveRange(batches);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    $"{batches.Count} invoice history record(s) " +
                    "were deleted successfully. Actual invoices " +
                    "and material quantities were not changed.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to delete the selected invoice history " +
                    "records. No partial deletion was saved.";
            }

            return RedirectToAction(nameof(History));
        }

        [HttpGet]
        public async Task<IActionResult> ErrorReport(
            int batchId,
            int page = 1,
            int pageSize = 50)
        {
            page = Math.Max(page, 1);

            var allowedPageSizes =
                new[] { 25, 50, 100, 200 };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 50;
            }

            var query = _context.VyaparInvoiceImportRows
                .AsNoTracking()
                .Where(x => x.BatchId == batchId);

            var summary = await query
                .GroupBy(x => 1)
                .Select(group => new
                {
                    TotalRows = group.Count(),
                    SuccessRows = group.Count(x =>
                        x.Status == "Success"),
                    DeletedRows = group.Count(x =>
                        x.Status == "Deleted"),
                    PendingRows = group.Count(x =>
                        x.Status == null ||
                        (x.Status != "Success" &&
                         x.Status != "Deleted")),
                    TotalQty = group.Sum(x => x.InvoiceQty)
                })
                .FirstOrDefaultAsync();

            var totalRows = summary?.TotalRows ?? 0;

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalRows / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var rows = await query
                .OrderBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.BatchId = batchId;
            ViewBag.TotalRows = totalRows;
            ViewBag.SuccessRows = summary?.SuccessRows ?? 0;
            ViewBag.PendingRows = summary?.PendingRows ?? 0;
            ViewBag.DeletedRows = summary?.DeletedRows ?? 0;
            ViewBag.TotalQty = summary?.TotalQty ?? 0m;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            return View(rows);
        }

        [HttpGet]
        public async Task<IActionResult> InvoiceStatus(
            string? search,
            int page = 1,
            int pageSize = 50)
        {
            page = Math.Max(page, 1);

            var allowedPageSizes =
                new[] { 10, 25, 50, 100, 200 };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 50;
            }

            search = search?.Trim();

            var query = _context.Materials
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    (x.MaterialNo ?? "").Contains(search) ||
                    (x.LineItem != null &&
                     x.LineItem.Contains(search)) ||
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.PONumber.Contains(search)) ||
                    (x.ShortDescription != null &&
                     x.ShortDescription.Contains(search)));
            }

            var summary = await query
                .Select(x => new
                {
                    FullyInvoiced =
                        x.InvoiceStatus == "Fully Invoiced" ? 1 : 0,
                    PartialInvoiced =
                        x.InvoiceStatus == "Partially Invoiced" ? 1 : 0,
                    NotInvoiced =
                        x.InvoiceStatus == null ||
                        x.InvoiceStatus == "" ||
                        x.InvoiceStatus == "Not Invoiced" ? 1 : 0,
                    FullyDispatched =
                        x.DispatchStatus == "Dispatched" ||
                        x.DispatchStatus == "Completed" ? 1 : 0,
                    PartialDispatched =
                        x.DispatchStatus == "Partial" ||
                        x.DispatchStatus == "Running" ? 1 : 0,
                    PendingDispatch =
                        x.DispatchStatus == null ||
                        x.DispatchStatus == "" ||
                        x.DispatchStatus == "Pending" ? 1 : 0,
                    x.Qty,
                    x.InvoiceQty,
                    BalanceQty = x.BalanceQty > 0
                        ? x.BalanceQty
                        : 0,
                    x.DispatchQty,
                    PendingQty = x.PendingQty > 0
                        ? x.PendingQty
                        : 0
                })
                .GroupBy(x => 1)
                .Select(group => new
                {
                    TotalMaterials = group.Count(),
                    FullyInvoiced = group.Sum(x => x.FullyInvoiced),
                    PartialInvoiced = group.Sum(x => x.PartialInvoiced),
                    NotInvoiced = group.Sum(x => x.NotInvoiced),
                    FullyDispatched = group.Sum(x => x.FullyDispatched),
                    PartialDispatched = group.Sum(x => x.PartialDispatched),
                    PendingDispatch = group.Sum(x => x.PendingDispatch),
                    TotalPOQty = group.Sum(x => x.Qty),
                    TotalInvoiceQty = group.Sum(x => x.InvoiceQty),
                    TotalBalanceQty = group.Sum(x => x.BalanceQty),
                    TotalDispatchQty = group.Sum(x => x.DispatchQty),
                    TotalPendingDispatchQty = group.Sum(x => x.PendingQty)
                })
                .FirstOrDefaultAsync();

            var totalMaterials =
                summary?.TotalMaterials ?? 0;

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalMaterials / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var materials = await query
                .Include(x => x.PurchaseOrder)
                .OrderByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.TotalMaterials = totalMaterials;
            ViewBag.FullyInvoiced = summary?.FullyInvoiced ?? 0;
            ViewBag.PartialInvoiced = summary?.PartialInvoiced ?? 0;
            ViewBag.NotInvoiced = summary?.NotInvoiced ?? 0;
            ViewBag.FullyDispatched = summary?.FullyDispatched ?? 0;
            ViewBag.PartialDispatched = summary?.PartialDispatched ?? 0;
            ViewBag.PendingDispatch = summary?.PendingDispatch ?? 0;
            ViewBag.TotalPOQty = summary?.TotalPOQty ?? 0m;
            ViewBag.TotalInvoiceQty = summary?.TotalInvoiceQty ?? 0m;
            ViewBag.TotalBalanceQty = summary?.TotalBalanceQty ?? 0m;
            ViewBag.TotalDispatchQty = summary?.TotalDispatchQty ?? 0m;
            ViewBag.TotalPendingDispatchQty =
                summary?.TotalPendingDispatchQty ?? 0m;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            return View(materials);
        }

        private void AddImportRow(
            int batchId,
            string? poNumber,
            string? lineItem,
            string? materialNumber,
            decimal invoiceQty,
            string? invoiceNumber,
            string? status,
            string? errorMessage)
        {
            _context.VyaparInvoiceImportRows.Add(new VyaparInvoiceImportRow
            {
                BatchId = batchId,
                PONumber = poNumber ?? "",
                LineItem = lineItem ?? "",
                MaterialNumber = materialNumber ?? "",
                InvoiceQty = invoiceQty,
                InvoiceNumber = invoiceNumber ?? "",
                Status = status ?? "Pending",
                ErrorMessage = errorMessage
            });
        }

        private InvoiceImportPreviewViewModel ExtractFromPdf(
            string filePath)
        {
            var text =
                NormalizeText(
                    ExtractPdfText(filePath));

            var model =
                new InvoiceImportPreviewViewModel
                {
                    InvoiceNumber =
                        ExtractInvoiceNumber(text),

                    InvoiceDate =
                        ExtractInvoiceDate(text),

                    CustomerName =
                        GetCustomerName(text),

                    Items =
                        ParseInvoiceRowsFromText(text)
                };

            return model;
        }

        // ============================================================
        // SAFE INVOICE NUMBER EXTRACTION
        // Supports:
        // Invoice No. FY26-27/547
        // Invoice No. Date
        // FY26-27/547 15-07-2026
        // ============================================================

        private static string ExtractInvoiceNumber(
            string text)
        {
            var lines =
                text.Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(x =>
                        Regex.Replace(
                                x,
                                @"\s+",
                                " ")
                            .Trim())
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Take(60)
                    .ToList();

            for (var index = 0;
                 index < lines.Count;
                 index++)
            {
                var line =
                    lines[index];

                if (!Regex.IsMatch(
                        line,
                        @"Invoice\s*(?:No|Number|#)",
                        RegexOptions.IgnoreCase))
                {
                    continue;
                }

                var afterLabel =
                    Regex.Replace(
                        line,
                        @"^.*?Invoice\s*" +
                        @"(?:No|Number|#)\.?\s*" +
                        @"[:\-]?\s*",
                        "",
                        RegexOptions.IgnoreCase);

                afterLabel =
                    Regex.Split(
                        afterLabel,
                        @"\b(?:Invoice\s+)?Date\b",
                        RegexOptions.IgnoreCase)
                    .FirstOrDefault()
                    ?.Trim() ?? "";

                var sameLineCandidate =
                    GetFirstInvoiceNumberCandidate(
                        afterLabel);

                if (!IsInvalidInvoiceNumber(
                        sameLineCandidate))
                {
                    return sameLineCandidate;
                }

                // Many invoice PDFs print invoice number and date
                // together on the next visual line.
                for (var offset = 1;
                     offset <= 3 &&
                     index + offset < lines.Count;
                     offset++)
                {
                    var nextLine =
                        lines[index + offset];

                    var nextCandidate =
                        GetFirstInvoiceNumberCandidate(
                            nextLine);

                    if (!IsInvalidInvoiceNumber(
                            nextCandidate))
                    {
                        return nextCandidate;
                    }
                }
            }

            // Final fallback over flattened header text.
            var headerText =
                string.Join(
                    " ",
                    lines.Take(30));

            var fallbackMatch =
                Regex.Match(
                    headerText,
                    @"Invoice\s*(?:No|Number|#)\.?" +
                    @".{0,80}?" +
                    @"(?<value>[A-Za-z0-9]" +
                    @"[A-Za-z0-9\/_-]{2,})",
                    RegexOptions.IgnoreCase);

            if (fallbackMatch.Success)
            {
                var candidate =
                    fallbackMatch.Groups["value"]
                        .Value
                        .Trim();

                if (!IsInvalidInvoiceNumber(
                        candidate))
                {
                    return candidate;
                }
            }

            return "";
        }

        private static string GetFirstInvoiceNumberCandidate(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var match =
                Regex.Match(
                    value.Trim(),
                    @"(?<value>[A-Za-z0-9]" +
                    @"[A-Za-z0-9\/_-]{2,})");

            if (!match.Success)
            {
                return "";
            }

            return match.Groups["value"]
                .Value
                .Trim();
        }

        private static bool IsInvalidInvoiceNumber(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var normalized =
                Regex.Replace(
                        value.Trim(),
                        @"[^A-Za-z0-9]",
                        "")
                    .ToUpperInvariant();

            if (normalized.Length < 3)
            {
                return true;
            }

            var invalidValues =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "DATE",
                    "INVOICEDATE",
                    "INVOICE",
                    "NUMBER",
                    "NO",
                    "TAXINVOICE",
                    "BILLTO",
                    "GSTIN",
                    "ORIGINAL",
                    "DUPLICATE",
                    "TRIPLICATE",
                    "PLACEOFSUPPLY"
                };

            if (invalidValues.Contains(
                    normalized))
            {
                return true;
            }

            if (Regex.IsMatch(
                    value.Trim(),
                    @"^\d{1,2}[\.\-/]\d{1,2}" +
                    @"[\.\-/]\d{2,4}$"))
            {
                return true;
            }

            // Invoice number must contain at least one digit.
            if (!value.Any(char.IsDigit))
            {
                return true;
            }

            return false;
        }

        private static string GetInvoiceNumberFromFileName(
            string? fileName)
        {
            var name =
                Path.GetFileNameWithoutExtension(
                    fileName ?? "")
                    .Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }

            var match =
                Regex.Match(
                    name,
                    @"(?:Tax\s*)?Invoice" +
                    @"[\s_-]*(?<value>.+)$",
                    RegexOptions.IgnoreCase);

            var candidate =
                match.Success
                    ? match.Groups["value"]
                        .Value
                        .Trim(' ', '_', '-')
                    : name;

            candidate =
                Regex.Replace(
                    candidate,
                    @"\s+",
                    "-");

            candidate =
                Regex.Replace(
                    candidate,
                    @"[^A-Za-z0-9\/_-]",
                    "");

            return IsInvalidInvoiceNumber(
                candidate)
                ? ""
                : candidate;
        }

        // ============================================================
        // SAFE INVOICE DATE EXTRACTION
        // ============================================================

        private DateTime? ExtractInvoiceDate(
            string text)
        {
            var lines =
                text.Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(x =>
                        Regex.Replace(
                                x,
                                @"\s+",
                                " ")
                            .Trim())
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Take(70)
                    .ToList();

            var datePattern =
                @"(?<value>\d{1,2}[\.\-/]" +
                @"\d{1,2}[\.\-/]\d{2,4})";

            for (var index = 0;
                 index < lines.Count;
                 index++)
            {
                var line =
                    lines[index];

                var hasInvoiceDateLabel =
                    Regex.IsMatch(
                        line,
                        @"Invoice\s*Date",
                        RegexOptions.IgnoreCase);

                var hasDateHeader =
                    Regex.IsMatch(
                        line,
                        @"(?:^|\s)Date(?:$|\s)",
                        RegexOptions.IgnoreCase);

                var hasInvoiceNumberHeader =
                    Regex.IsMatch(
                        line,
                        @"Invoice\s*(?:No|Number|#)",
                        RegexOptions.IgnoreCase);

                if (!hasInvoiceDateLabel &&
                    !(hasDateHeader &&
                      hasInvoiceNumberHeader) &&
                    !Regex.IsMatch(
                        line,
                        @"^\s*Date\s*$",
                        RegexOptions.IgnoreCase))
                {
                    continue;
                }

                var sameLineDate =
                    TryExtractValidDateFromLine(
                        line,
                        datePattern);

                if (sameLineDate.HasValue)
                {
                    return sameLineDate;
                }

                for (var offset = 1;
                     offset <= 3 &&
                     index + offset < lines.Count;
                     offset++)
                {
                    var nextLineDate =
                        TryExtractValidDateFromLine(
                            lines[index + offset],
                            datePattern);

                    if (nextLineDate.HasValue)
                    {
                        return nextLineDate;
                    }
                }
            }

            return null;
        }

        private DateTime? TryExtractValidDateFromLine(
            string line,
            string datePattern)
        {
            var matches =
                Regex.Matches(
                    line,
                    datePattern,
                    RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var parsedDate =
                    ParseDate(
                        match.Groups["value"]
                            .Value);

                if (parsedDate.HasValue)
                {
                    return parsedDate;
                }
            }

            return null;
        }

        // ============================================================
        // MULTI-LINE PDF TABLE PARSER
        //
        // The reference invoice prints GST and Amount in separate
        // visual lines. Therefore the complete PDF text is flattened,
        // then each invoice row is captured as one logical block.
        // ============================================================

        private List<InvoiceImportItemViewModel>
            ParseInvoiceRowsFromText(
                string text)
        {
            var result =
                new List<InvoiceImportItemViewModel>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var flattenedText =
                Regex.Replace(
                        text,
                        @"\s+",
                        " ")
                    .Trim();

            const string datePattern =
                @"\d{1,2}[\.\-/]\d{1,2}" +
                @"[\.\-/]\d{2,4}";

            const string rowStartPattern =
                @"(?<!\d)\d{1,4}\s+" +
                datePattern +
                @"\s+[A-Za-z0-9\/_-]{6,20}" +
                @"\s+(?:N[\s-]*)?\d{1,6}" +
                @"\s+[A-Za-z0-9_-]{5,20}\s+";

            var rowPattern =
                @"(?<row>" +
                rowStartPattern +
                @".*?)" +
                @"(?=" +
                rowStartPattern +
                @"|\s+Total\s+\d" +
                @"|\s+Invoice\s+Amount\s+in\s+Words" +
                @"|\z)";

            var rowMatches =
                Regex.Matches(
                    flattenedText,
                    rowPattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            foreach (Match rowMatch in rowMatches)
            {
                var rowText =
                    rowMatch.Groups["row"]
                        .Value
                        .Trim();

                var item =
                    ParseInvoiceRowBlock(
                        rowText);

                if (item != null)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private InvoiceImportItemViewModel?
            ParseInvoiceRowBlock(
                string rowText)
        {
            if (string.IsNullOrWhiteSpace(
                    rowText))
            {
                return null;
            }

            rowText =
                Regex.Replace(
                        rowText,
                        @"\s+",
                        " ")
                    .Trim();

            var leadingMatch =
                Regex.Match(
                    rowText,
                    @"^(?<sl>\d{1,4})\s+" +
                    @"(?<podate>\d{1,2}[\.\-/]" +
                    @"\d{1,2}[\.\-/]\d{2,4})\s+" +
                    @"(?<po>[A-Za-z0-9\/_-]{6,20})\s+" +
                    @"(?<item>(?:N[\s-]*)?\d{1,6})\s+" +
                    @"(?<material>[A-Za-z0-9_-]{5,20})\s+" +
                    @"(?<rest>.+)$",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (!leadingMatch.Success)
            {
                return null;
            }

            var remainder =
                leadingMatch.Groups["rest"]
                    .Value
                    .Trim();

            // The final 6-10 digit number before qty is HSN/SAC.
            var hsnMatches =
                Regex.Matches(
                    remainder,
                    @"(?<![A-Za-z0-9])" +
                    @"(?<hsn>\d{6,10})" +
                    @"(?![A-Za-z0-9])");

            if (hsnMatches.Count == 0)
            {
                return null;
            }

            var hsnMatch =
                hsnMatches[
                    hsnMatches.Count - 1];

            var description =
                remainder[
                    ..hsnMatch.Index]
                    .Trim();

            var hsnSacCode =
                hsnMatch.Groups["hsn"]
                    .Value
                    .Trim();

            var numericPart =
                remainder[
                    (hsnMatch.Index +
                     hsnMatch.Length)..]
                    .Trim();

            // Remove GST rate text such as (18%) or 18%.
            numericPart =
                Regex.Replace(
                    numericPart,
                    @"\(?\s*\d+(?:\.\d+)?" +
                    @"\s*%\s*\)?",
                    " ");

            numericPart =
                numericPart
                    .Replace("₹", " ")
                    .Replace(
                        "Rs.",
                        " ",
                        StringComparison.OrdinalIgnoreCase)
                    .Replace(
                        "Rs",
                        " ",
                        StringComparison.OrdinalIgnoreCase);

            var numericValues =
                Regex.Matches(
                        numericPart,
                        @"-?\d[\d,]*(?:\.\d+)?")
                    .Select(x =>
                        ParseDecimal(
                            x.Value))
                    .ToList();

            if (numericValues.Count < 2)
            {
                return null;
            }

            var invoiceQty =
                numericValues[0];

            var priceUnit =
                numericValues[1];

            if (invoiceQty <= 0 ||
                priceUnit < 0)
            {
                return null;
            }

            var taxableAmount =
                decimal.Round(
                    invoiceQty *
                    priceUnit,
                    2,
                    MidpointRounding.AwayFromZero);

            var trailingValues =
                numericValues
                    .Skip(2)
                    .Where(x => x >= 0)
                    .ToList();

            decimal amount;
            decimal gstAmount;

            // The final invoice amount is normally greater than or
            // equal to Qty × Rate. This also ignores a GST value that
            // visually belongs to the next row.
            var amountCandidates =
                trailingValues
                    .Where(x =>
                        x >= taxableAmount)
                    .ToList();

            if (amountCandidates.Count > 0)
            {
                amount =
                    amountCandidates.Max();

                gstAmount =
                    Math.Max(
                        0,
                        amount -
                        taxableAmount);
            }
            else if (trailingValues.Count > 0)
            {
                // When only GST was extracted, rebuild the amount.
                gstAmount =
                    trailingValues.Sum();

                amount =
                    taxableAmount +
                    gstAmount;
            }
            else
            {
                gstAmount = 0;
                amount = taxableAmount;
            }

            return new InvoiceImportItemViewModel
            {
                PODate =
                    ParseDate(
                        leadingMatch
                            .Groups["podate"]
                            .Value),

                PONumber =
                    leadingMatch
                        .Groups["po"]
                        .Value
                        .Trim(),

                LineItem =
                    NormalizeLineItem(
                        leadingMatch
                            .Groups["item"]
                            .Value),

                MaterialNo =
                    leadingMatch
                        .Groups["material"]
                        .Value
                        .Trim(),

                Description =
                    CleanText(
                        description),

                HsnSacCode =
                    hsnSacCode,

                InvoiceQty =
                    invoiceQty,

                PriceUnit =
                    priceUnit,

                GstAmount =
                    decimal.Round(
                        gstAmount,
                        2,
                        MidpointRounding.AwayFromZero),

                Amount =
                    decimal.Round(
                        amount,
                        2,
                        MidpointRounding.AwayFromZero)
            };
        }

        private InvoiceImportPreviewViewModel ExtractFromExcel(string filePath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var model = new InvoiceImportPreviewViewModel();

            using var stream = System.IO.File.Open(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            using var reader = ExcelReaderFactory.CreateReader(stream);

            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });

            if (dataSet.Tables.Count == 0)
            {
                return model;
            }

            var table = dataSet.Tables[0];

            model.InvoiceNumber = FindValueNearLabel(table, "Invoice No");
            model.InvoiceDate = ParseDate(FindValueNearLabel(table, "Date"));
            model.CustomerName = FindValueNearLabel(table, "Bill To");

            if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
            {
                model.InvoiceNumber = "INV-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            }

            int headerRowIndex = FindInvoiceHeaderRow(table);

            if (headerRowIndex < 0)
            {
                return model;
            }

            var columnMap = BuildColumnMap(table, headerRowIndex);

            int poDateCol = GetColumn(columnMap, "PO/DC Date", "PO Date", "Date");
            int poNoCol = GetColumn(columnMap, "PO/DC NO", "PO/ DC NO", "PO Number", "PO No", "PONumber");
            int itemCol = GetColumn(columnMap, "ITEM NO", "Item No", "Line Item");
            int materialCol = GetColumn(columnMap, "MATERIAL", "Material No", "Material Number", "Item Code");
            int descCol = GetColumn(columnMap, "DESCRIPTION", "Material Description");
            int hsnCol = GetColumn(columnMap, "HSN/SAC", "HSN", "HSN SAC");
            int qtyCol = GetColumn(columnMap, "QTY", "Qty", "Quantity", "Invoice Qty");
            int priceCol = GetColumn(columnMap, "Price/Unit", "Price Unit", "Rate");
            int gstCol = GetColumn(columnMap, "GST");
            int amountCol = GetColumn(columnMap, "Amount");

            for (int r = headerRowIndex + 1; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];

                string poNo = GetCell(row, poNoCol);
                string material = GetCell(row, materialCol);
                decimal qty = ParseDecimal(GetCell(row, qtyCol));

                string rowText = string.Join(" ", row.ItemArray.Select(x => x?.ToString() ?? ""));

                if (rowText.Contains("Total", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(poNo) &&
                    string.IsNullOrWhiteSpace(material) &&
                    qty == 0)
                {
                    continue;
                }

                model.Items.Add(new InvoiceImportItemViewModel
                {
                    PODate = ParseDate(GetCell(row, poDateCol)),
                    PONumber = poNo,
                    LineItem = NormalizeLineItem(GetCell(row, itemCol)),
                    MaterialNo = material,
                    Description = GetCell(row, descCol),
                    HsnSacCode = GetCell(row, hsnCol),
                    InvoiceQty = qty,
                    PriceUnit = ParseDecimal(GetCell(row, priceCol)),
                    GstAmount = ParseDecimal(GetCell(row, gstCol)),
                    Amount = ParseDecimal(GetCell(row, amountCol))
                });
            }

            return model;
        }

        private static string ExtractPdfText(string filePath)
        {
            var sb = new StringBuilder();

            using var document = PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                var words = page.GetWords().ToList();

                if (words.Count == 0)
                {
                    sb.AppendLine(page.Text);
                    continue;
                }

                var lines = words
                    .GroupBy(w => Math.Round(w.BoundingBox.Top / 5) * 5)
                    .OrderByDescending(g => g.Key);

                foreach (var line in lines)
                {
                    var lineText = string.Join(" ",
                        line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));

                    sb.AppendLine(lineText);
                }
            }

            return sb.ToString();
        }

        private static int FindInvoiceHeaderRow(DataTable table)
        {
            for (int r = 0; r < table.Rows.Count; r++)
            {
                string rowText = string.Join(" ",
                    table.Rows[r].ItemArray.Select(x => x?.ToString() ?? ""));

                string normalized = NormalizeHeader(rowText);

                if (normalized.Contains("PODCNO") &&
                    normalized.Contains("MATERIAL") &&
                    normalized.Contains("QTY"))
                {
                    return r;
                }

                if (normalized.Contains("PONUMBER") &&
                    normalized.Contains("MATERIAL") &&
                    normalized.Contains("QTY"))
                {
                    return r;
                }
            }

            return -1;
        }

        private static Dictionary<string, int> BuildColumnMap(DataTable table, int headerRowIndex)
        {
            var map = new Dictionary<string, int>();

            var row = table.Rows[headerRowIndex];

            for (int c = 0; c < table.Columns.Count; c++)
            {
                string header = row[c]?.ToString()?.Trim() ?? "";

                if (!string.IsNullOrWhiteSpace(header))
                {
                    string normalized = NormalizeHeader(header);

                    map.TryAdd(
                        normalized,
                        c);
                }
            }

            return map;
        }

        private static int GetColumn(Dictionary<string, int> map, params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                string key = NormalizeHeader(name);

                if (map.TryGetValue(key, out int col))
                {
                    return col;
                }
            }

            return -1;
        }

        private static string GetCell(DataRow row, int column)
        {
            if (column < 0 || column >= row.ItemArray.Length)
            {
                return "";
            }

            return row[column]?.ToString()?.Trim() ?? "";
        }

        private static string FindValueNearLabel(DataTable table, string label)
        {
            string normalizedLabel = NormalizeHeader(label);

            for (int r = 0; r < table.Rows.Count; r++)
            {
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    string cell = table.Rows[r][c]?.ToString()?.Trim() ?? "";

                    if (NormalizeHeader(cell).Contains(normalizedLabel))
                    {
                        if (c + 1 < table.Columns.Count)
                        {
                            string right = table.Rows[r][c + 1]?.ToString()?.Trim() ?? "";

                            if (!string.IsNullOrWhiteSpace(right))
                            {
                                return right;
                            }
                        }

                        if (r + 1 < table.Rows.Count)
                        {
                            string below = table.Rows[r + 1][c]?.ToString()?.Trim() ?? "";

                            if (!string.IsNullOrWhiteSpace(below))
                            {
                                return below;
                            }
                        }
                    }
                }
            }

            return "";
        }

        private static string GetCustomerName(string text)
        {
            var match = Regex.Match(
                text,
                @"Bill\s*To\s+(?<name>.+?)\s+(?:Phase|GSTIN|State)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success)
            {
                return CleanText(match.Groups["name"].Value);
            }

            return "";
        }

        private static string GetSingleValue(string text, string pattern)
        {
            var match = Regex.Match(
                text,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!match.Success)
            {
                return "";
            }

            if (match.Groups["value"] != null && match.Groups["value"].Success)
            {
                return CleanText(match.Groups["value"].Value);
            }

            if (match.Groups.Count > 1)
            {
                return CleanText(match.Groups[1].Value);
            }

            return "";
        }

        private static string NormalizeKey(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeLineItem(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            value = value.Trim();

            value = value.Replace("N-", "", StringComparison.OrdinalIgnoreCase);
            value = value.Replace("N", "", StringComparison.OrdinalIgnoreCase);
            value = value.Replace("-", "");

            value = Regex.Replace(value, @"\D", "");

            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            return "N-" + value;
        }

        private static string GetInvoiceStatus(decimal poQty, decimal invoiceQty, decimal balanceQty)
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

        private static DateTime? ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            var formats = new[]
            {
                "dd.MM.yyyy",
                "dd-MM-yyyy",
                "dd/MM/yyyy",
                "d.M.yyyy",
                "d-M-yyyy",
                "d/M/yyyy",
                "dd.MM.yy",
                "dd-MM-yy",
                "dd/MM/yy",
                "d.M.yy",
                "d-M-yy",
                "d/M/yy",
                "yyyy-MM-dd"
            };

            if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                return date;
            }

            if (DateTime.TryParse(value, out date))
            {
                return date;
            }

            return null;
        }

        private static decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            value = value
                .Replace("₹", "")
                .Replace("Rs.", "")
                .Replace("Rs", "")
                .Replace(",", "")
                .Replace("(", "")
                .Replace(")", "")
                .Trim();

            if (decimal.TryParse(
                    value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var result))
            {
                return result;
            }

            return 0;
        }

        private static string NormalizeHeader(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text = text.Replace("\r", "\n");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n\s+", "\n");
            text = Regex.Replace(text, @"\n{2,}", "\n");

            return text.Trim();
        }

        private static string CleanText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            value = value.Replace("\r", "\n");
            value = Regex.Replace(value, @"[ \t]+", " ");
            value = Regex.Replace(value, @"\n\s+", "\n");
            value = Regex.Replace(value, @"\n{2,}", "\n");

            return value.Trim();
        }
    }
}

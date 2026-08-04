using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using POTracker.ViewModels;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace POTracker.Controllers
{
    public partial class POImportController : ControllerBase
    {
        private const int MaxBatchFileCount = 100;

        private const long MaxPdfFileSize =
            20L * 1024L * 1024L;
        private readonly IWebHostEnvironment _environment;
        private readonly ApplicationDbContext _context;
        private readonly PendingInvoiceMatcher _pendingInvoiceMatcher;

        public POImportController(
            IWebHostEnvironment environment,
            ApplicationDbContext context,
            PendingInvoiceMatcher pendingInvoiceMatcher)
        {
            _environment = environment;
            _context = context;
            _pendingInvoiceMatcher = pendingInvoiceMatcher;
        }

        // ============================================================
        // REUSABLE SINGLE PO DATABASE SAVE
        // ============================================================

        private async Task<PurchaseOrderSaveResult>
            SavePurchaseOrderAsync(
                POImportPreviewViewModel model)
        {
            if (string.IsNullOrWhiteSpace(
                    model.PurchaseOrderNo))
            {
                throw new InvalidOperationException(
                    "Purchase order number is missing.");
            }

            if (model.Items == null ||
                model.Items.Count == 0)
            {
                throw new InvalidOperationException(
                    "No material items found to save.");
            }

            // Some WEG PO formats do not provide a material number.
            // Line item + quantity are enough to save a valid PO row.
            var validItems = model.Items
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.LineItem) &&
                    x.Quantity > 0)
                .ToList();

            if (validItems.Count == 0)
            {
                throw new InvalidOperationException(
                    "No valid material rows were found to save.");
            }

            model.PurchaseOrderNo =
                model.PurchaseOrderNo.Trim();

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var assignedDashboard =
                    GetAssignedDashboard(
                        model.BuyerContactPerson);

                var purchaser =
                    await _context.Purchasers
                        .FirstOrDefaultAsync(x =>
                            x.PurchaserName ==
                            "WEG Industries (India) PVT LTD");

                if (purchaser == null)
                {
                    purchaser = new Purchaser
                    {
                        PurchaserName =
                            "WEG Industries (India) PVT LTD",

                        ContactPerson =
                            model.BuyerContactPerson
                    };

                    _context.Purchasers.Add(purchaser);

                    await _context.SaveChangesAsync();
                }
                else
                {
                    purchaser.ContactPerson =
                        model.BuyerContactPerson;

                    _context.Purchasers.Update(purchaser);

                    await _context.SaveChangesAsync();
                }

                var purchaseOrder =
                    await _context.PurchaseOrders
                        .FirstOrDefaultAsync(x =>
                            x.PONumber ==
                            model.PurchaseOrderNo);

                var wasCreated =
                    purchaseOrder == null;

                if (purchaseOrder == null)
                {
                    purchaseOrder = new PurchaseOrder
                    {
                        PurchaserId = purchaser.Id,

                        PONumber =
                            model.PurchaseOrderNo,

                        // Date of Order from the PDF.
                        PODate =
                            model.PODate,

                        // Date of emission from the PDF is treated
                        // as the PO received date.
                        ReceivedDate =
                            model.ReceivedDate,

                        BuyerContactPerson =
                            model.BuyerContactPerson,

                        PdfFilePath =
                            model.UploadedPdfPath,

                        CreatedDate =
                            DateTime.Now
                    };

                    _context.PurchaseOrders.Add(
                        purchaseOrder);

                    await _context.SaveChangesAsync();
                }
                else
                {
                    purchaseOrder.PurchaserId =
                        purchaser.Id;

                    purchaseOrder.PODate =
                        model.PODate;

                    purchaseOrder.ReceivedDate =
                        model.ReceivedDate;

                    purchaseOrder.BuyerContactPerson =
                        model.BuyerContactPerson;

                    purchaseOrder.PdfFilePath =
                        model.UploadedPdfPath;

                    _context.PurchaseOrders.Update(
                        purchaseOrder);

                    await _context.SaveChangesAsync();
                }

                foreach (var item in validItems)
                {
                    string? drawingFilePath = null;

                    if (item.DrawingFile != null &&
                        item.DrawingFile.Length > 0)
                    {
                        drawingFilePath =
                            await SaveImportFile(
                                item.DrawingFile,
                                "drawings");
                    }

                    var normalizedLineItem =
                        NormalizeLineItem(
                            item.LineItem);

                    var existingMaterial =
                        await _context.Materials
                            .FirstOrDefaultAsync(x =>
                                x.PurchaseOrderId ==
                                purchaseOrder.Id &&
                                x.LineItem ==
                                normalizedLineItem);

                    if (existingMaterial != null)
                    {
                        existingMaterial.MaterialNo =
                            string.IsNullOrWhiteSpace(item.MaterialNo)
                                ? null
                                : item.MaterialNo.Trim();

                        existingMaterial.ItemType =
                            GetItemTypeFromLineItem(item.LineItem);

                        existingMaterial.HsnSacCode =
                            item.HsnSacCode?.Trim();

                        existingMaterial.ShortDescription =
                            item.ShortDescription?.Trim();

                        existingMaterial.BriefDescription =
                            item.FullDescription?.Trim();

                        existingMaterial.Qty =
                            item.Quantity;

                        existingMaterial.Unit =
                            item.Unit?.Trim();

                        existingMaterial.Dep =
                            item.Dep?.Trim();

                        existingMaterial.PendingQty =
                            item.Quantity -
                            existingMaterial.DispatchQty;

                        if (existingMaterial.PendingQty < 0)
                        {
                            existingMaterial.PendingQty = 0;
                        }

                        existingMaterial.BalanceQty =
                            item.Quantity -
                            existingMaterial.InvoiceQty;

                        if (existingMaterial.BalanceQty < 0)
                        {
                            existingMaterial.BalanceQty = 0;
                        }

                        existingMaterial.InvoiceStatus =
                            GetInvoiceStatus(
                                existingMaterial.Qty,
                                existingMaterial.InvoiceQty,
                                existingMaterial.BalanceQty);

                        existingMaterial.Cost =
                            item.ValueOfGoods;

                        existingMaterial.ValueOfGoods =
                            item.ValueOfGoods;

                        existingMaterial.PriceUnit =
                            item.PriceUnit?.Trim();

                        existingMaterial.TotalValue =
                            item.TotalValue;

                        existingMaterial.PODate =
                            model.PODate;

                        existingMaterial.ReceivingDate =
                            model.ReceivedDate;

                        existingMaterial.DeliveryDate =
                            item.DeliveryDate;

                        existingMaterial.DrawingNo =
                            item.DrawingNo?.Trim();

                        existingMaterial.DrawingVersion =
                            item.DrawingNo?.Trim();

                        if (!string.IsNullOrWhiteSpace(
                                drawingFilePath))
                        {
                            existingMaterial.DrawingFilePath =
                                drawingFilePath;
                        }

                        existingMaterial.PDFFilePath =
                            model.UploadedPdfPath;

                        if (string.IsNullOrWhiteSpace(
                                existingMaterial.ProcessStatus))
                        {
                            existingMaterial.ProcessStatus =
                                "Pending";
                        }

                        if (string.IsNullOrWhiteSpace(
                                existingMaterial.DispatchStatus))
                        {
                            existingMaterial.DispatchStatus =
                                "Pending";
                        }

                        existingMaterial.AssignedDashboard =
                            assignedDashboard;

                        SetMaterialProcessDates(
                            existingMaterial);

                        _context.Materials.Update(
                            existingMaterial);
                    }
                    else
                    {
                        var material = new Material
                        {
                            PurchaseOrderId =
                                purchaseOrder.Id,

                            LineItem =
                                normalizedLineItem,

                            MaterialNo =
                                string.IsNullOrWhiteSpace(item.MaterialNo)
                                    ? null
                                    : item.MaterialNo.Trim(),

                            ItemType =
                                GetItemTypeFromLineItem(item.LineItem),

                            HsnSacCode =
                                item.HsnSacCode?.Trim(),

                            ShortDescription =
                                item.ShortDescription?.Trim(),

                            BriefDescription =
                                item.FullDescription?.Trim(),

                            Qty =
                                item.Quantity,

                            Unit =
                                item.Unit?.Trim(),

                            Dep =
                                item.Dep?.Trim(),

                            DispatchQty = 0,

                            PendingQty =
                                item.Quantity,

                            StockQty = 0,

                            InvoiceQty = 0,

                            BalanceQty =
                                item.Quantity,

                            InvoiceStatus =
                                "Not Invoiced",

                            Cost =
                                item.ValueOfGoods,

                            ValueOfGoods =
                                item.ValueOfGoods,

                            PriceUnit =
                                item.PriceUnit?.Trim(),

                            TotalValue =
                                item.TotalValue,

                            PODate =
                                model.PODate,

                            ReceivingDate =
                                model.ReceivedDate,

                            DeliveryDate =
                                item.DeliveryDate,

                            DrawingNo =
                                item.DrawingNo?.Trim(),

                            DrawingVersion =
                                item.DrawingNo?.Trim(),

                            DrawingFilePath =
                                drawingFilePath,

                            PDFFilePath =
                                model.UploadedPdfPath,

                            ProcessStatus =
                                "Pending",

                            DispatchStatus =
                                "Pending",

                            AssignedDashboard =
                                assignedDashboard,

                            CreatedDate =
                                DateTime.Now
                        };

                        SetMaterialProcessDates(material);

                        _context.Materials.Add(material);
                    }
                }

                await _context.SaveChangesAsync();

                var matchedInvoiceRows =
                    await _pendingInvoiceMatcher
                        .MatchPendingInvoicesForPurchaseOrderAsync(
                            purchaseOrder.Id);

                await transaction.CommitAsync();

                return new PurchaseOrderSaveResult
                {
                    WasCreated = wasCreated,
                    PurchaseOrderId = purchaseOrder.Id,
                    MaterialRowsSaved = validItems.Count,
                    MatchedInvoiceRows = matchedInvoiceRows
                };
            }
            catch
            {
                await transaction.RollbackAsync();

                _context.ChangeTracker.Clear();

                throw;
            }
        }

        // ============================================================
        // PROCESS ONE PO PDF FOR SINGLE OR BATCH IMPORT
        // ============================================================

        private async Task<POBatchImportFileViewModel>
            ProcessPdfFileAsync(IFormFile? pdfFile)
        {
            var result = new POBatchImportFileViewModel
            {
                OriginalFileName =
                    pdfFile == null
                        ? string.Empty
                        : Path.GetFileName(pdfFile.FileName),

                FileSize =
                    pdfFile?.Length ?? 0,

                IsSuccessful = false,

                IsSelected = false
            };

            // --------------------------------------------------------
            // CHECK WHETHER FILE EXISTS
            // --------------------------------------------------------

            if (pdfFile == null ||
                pdfFile.Length == 0)
            {
                result.ErrorMessage =
                    "The selected PDF file is empty.";

                return result;
            }

            // --------------------------------------------------------
            // CHECK FILE EXTENSION
            // --------------------------------------------------------

            var extension =
                Path.GetExtension(pdfFile.FileName);

            if (!string.Equals(
                    extension,
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage =
                    "Only PDF files are allowed.";

                return result;
            }

            // --------------------------------------------------------
            // CHECK INDIVIDUAL FILE SIZE
            // --------------------------------------------------------

            if (pdfFile.Length > MaxPdfFileSize)
            {
                result.ErrorMessage =
                    "The PDF exceeds the maximum allowed " +
                    "file size of 20 MB.";

                return result;
            }

            // --------------------------------------------------------
            // CREATE PO UPLOAD FOLDER
            // --------------------------------------------------------

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "po");

            Directory.CreateDirectory(uploadFolder);

            var safeFileName =
                Path.GetFileName(pdfFile.FileName);

            var storedFileName =
                $"{Guid.NewGuid():N}_{safeFileName}";

            var physicalFilePath = Path.Combine(
                uploadFolder,
                storedFileName);

            try
            {
                // ----------------------------------------------------
                // SAVE PDF INTO WWWROOT
                // ----------------------------------------------------

                await using (var stream = new FileStream(
                                 physicalFilePath,
                                 FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                // ----------------------------------------------------
                // EXTRACT PDF TEXT
                // ----------------------------------------------------

                var pdfText =
                    ExtractTextFromPdf(physicalFilePath);

                if (string.IsNullOrWhiteSpace(pdfText))
                {
                    DeleteFileIfExists(physicalFilePath);

                    result.ErrorMessage =
                        "The PDF does not contain readable text. " +
                        "Please upload a text-based PO PDF.";

                    return result;
                }

                // ----------------------------------------------------
                // EXTRACT PO HEADER AND MATERIALS
                // ----------------------------------------------------

                var purchaseOrderModel =
                    ExtractPOData(pdfText);

                purchaseOrderModel.UploadedPdfPath =
                    "/uploads/po/" + storedFileName;

                // ----------------------------------------------------
                // VALIDATE PO NUMBER
                // ----------------------------------------------------

                if (string.IsNullOrWhiteSpace(
                        purchaseOrderModel.PurchaseOrderNo))
                {
                    DeleteFileIfExists(physicalFilePath);

                    result.ErrorMessage =
                        "Purchase order number could not be " +
                        "detected from this PDF.";

                    return result;
                }

                purchaseOrderModel.PurchaseOrderNo =
                    purchaseOrderModel.PurchaseOrderNo.Trim();

                // ----------------------------------------------------
                // VALIDATE MATERIAL ROWS
                // ----------------------------------------------------

                if (purchaseOrderModel.Items == null ||
                    purchaseOrderModel.Items.Count == 0)
                {
                    DeleteFileIfExists(physicalFilePath);

                    result.ErrorMessage =
                        "No material rows were detected in this PDF. " +
                        "Please verify the PO PDF format.";

                    return result;
                }

                var validMaterialCount =
                    purchaseOrderModel.Items.Count(x =>
                        !string.IsNullOrWhiteSpace(x.LineItem) &&
                        x.Quantity > 0);

                if (validMaterialCount == 0)
                {
                    DeleteFileIfExists(physicalFilePath);

                    result.ErrorMessage =
                        "No valid material rows were found in this PDF.";

                    return result;
                }

                // ----------------------------------------------------
                // CHECK WHETHER PO ALREADY EXISTS
                // ----------------------------------------------------

                result.ExistsInDatabase =
                    await _context.PurchaseOrders
                        .AsNoTracking()
                        .AnyAsync(x =>
                            x.PONumber ==
                            purchaseOrderModel.PurchaseOrderNo);

                // ----------------------------------------------------
                // SUCCESS RESULT
                // ----------------------------------------------------

                result.PurchaseOrder =
                    purchaseOrderModel;

                result.IsSuccessful = true;

                result.IsSelected = true;

                result.ErrorMessage = null;

                return result;
            }
            catch (Exception ex)
            {
                DeleteFileIfExists(physicalFilePath);

                result.IsSuccessful = false;

                result.IsSelected = false;

                result.ErrorMessage =
                    "Unable to process this PO PDF. " +
                    ex.Message;

                return result;
            }
        }

        // ============================================================
        // MATERIAL PROCESS DATE
        // ============================================================

        private void SetMaterialProcessDates(
            Material material)
        {
            if (!material.DeliveryDate.HasValue)
            {
                return;
            }

            material.ProcessStartDate =
                material.DeliveryDate.Value.AddMonths(-1);

            material.AlertDate =
                material.ProcessStartDate;
        }

        // ============================================================
        // NORMALIZE LINE ITEM
        // ============================================================

        private string NormalizeLineItem(
            string? lineItem)
        {
            if (string.IsNullOrWhiteSpace(lineItem))
            {
                return "";
            }

            lineItem = lineItem.Trim();

            var match = Regex.Match(
                lineItem,
                @"^(?:(?<status>[NMC])[-\s]*)?(?<number>\d+)$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                var numberMatch = Regex.Match(lineItem, @"\d+");

                return numberMatch.Success
                    ? "N-" + numberMatch.Value
                    : lineItem;
            }

            var status = match.Groups["status"].Success
                ? match.Groups["status"].Value.ToUpperInvariant()
                : "N";

            return $"{status}-{match.Groups["number"].Value}";
        }

        // ============================================================
        // ITEM TYPE FROM NORMALIZED LINE ITEM
        // ============================================================

        private string GetItemTypeFromLineItem(string? lineItem)
        {
            if (string.IsNullOrWhiteSpace(lineItem))
            {
                return "N";
            }

            var match = Regex.Match(
                lineItem.Trim(),
                @"^(?<status>[NMC])(?:-|\s)",
                RegexOptions.IgnoreCase);

            return match.Success
                ? match.Groups["status"].Value.ToUpperInvariant()
                : "N";
        }

        // ============================================================
        // INVOICE STATUS
        // ============================================================

        private string GetInvoiceStatus(
            decimal qty,
            decimal invoiceQty,
            decimal balanceQty)
        {
            if (invoiceQty <= 0)
            {
                return "Not Invoiced";
            }

            if (balanceQty <= 0 ||
                invoiceQty >= qty)
            {
                return "Fully Invoiced";
            }

            return "Partially Invoiced";
        }

        // ============================================================
        // ASSIGNED DASHBOARD
        // ============================================================

        private string GetAssignedDashboard(
            string? buyerContactPerson)
        {
            if (string.IsNullOrWhiteSpace(
                    buyerContactPerson))
            {
                return "";
            }

            buyerContactPerson =
                buyerContactPerson.Trim();

            if (buyerContactPerson.Contains(
                    "Gokulnath",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Gokulnath";
            }

            if (buyerContactPerson.Contains(
                    "Paramaguru",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Paramaguru";
            }

            if (buyerContactPerson.Contains(
                    "Sam",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Sam";
            }

            if (buyerContactPerson.Contains(
                    "Abdul",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Abdul";
            }

            if (buyerContactPerson.Contains(
                    "Kalai",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Kalai";
            }

            return "";
        }

        // ============================================================
        // SAVE DRAWING FILE
        // ============================================================

        private async Task<string> SaveImportFile(
            IFormFile file,
            string folderName)
        {
            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                folderName);

            Directory.CreateDirectory(
                uploadFolder);

            var safeFileName =
                Path.GetFileName(file.FileName);

            var fileName =
                $"{Guid.NewGuid():N}_{safeFileName}";

            var filePath = Path.Combine(
                uploadFolder,
                fileName);

            await using (var stream =
                         new FileStream(
                             filePath,
                             FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return
                "/uploads/" +
                folderName +
                "/" +
                fileName;
        }

        // ============================================================
        // DELETE FAILED UPLOAD
        // ============================================================

        private void DeleteFileIfExists(
            string filePath)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch
            {
                // Do not interrupt the user response
                // if temporary-file cleanup fails.
            }
        }

        // ============================================================
        // EXTRACT PDF TEXT
        // ============================================================

        private string ExtractTextFromPdf(
            string filePath)
        {
            var stringBuilder =
                new StringBuilder();

            using var document =
                PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                var pageText =
                    ExtractPageTextByLines(page);

                if (string.IsNullOrWhiteSpace(pageText))
                {
                    pageText = page.Text;
                }

                stringBuilder.AppendLine(pageText);
                stringBuilder.AppendLine();
            }

            return stringBuilder.ToString();
        }

        // ============================================================
        // EXTRACT PAGE WORDS AS LINES
        // ============================================================

        private string ExtractPageTextByLines(
            Page page)
        {
            var words =
                page.GetWords().ToList();

            if (words.Count == 0)
            {
                return page.Text;
            }

            var lines =
                new List<PdfTextLine>();

            foreach (var word in words
                         .OrderByDescending(x =>
                             x.BoundingBox.Top)
                         .ThenBy(x =>
                             x.BoundingBox.Left))
            {
                var existingLine =
                    lines.FirstOrDefault(x =>
                        Math.Abs(
                            x.Top -
                            word.BoundingBox.Top) <= 4);

                if (existingLine == null)
                {
                    existingLine =
                        new PdfTextLine
                        {
                            Top =
                                word.BoundingBox.Top
                        };

                    lines.Add(existingLine);
                }

                existingLine.Words.Add(
                    (
                        word.BoundingBox.Left,
                        word.Text
                    ));
            }

            var stringBuilder =
                new StringBuilder();

            foreach (var line in lines
                         .OrderByDescending(x =>
                             x.Top))
            {
                var lineText =
                    string.Join(
                        " ",
                        line.Words
                            .OrderBy(x =>
                                x.Left)
                            .Select(x =>
                                x.Text));

                stringBuilder.AppendLine(
                    lineText);
            }

            return stringBuilder.ToString();
        }

        // ============================================================
        // EXTRACT PO HEADER AND MATERIALS
        // ============================================================

        private POImportPreviewViewModel ExtractPOData(
            string text)
        {
            text = NormalizePdfText(text);

            var model =
                new POImportPreviewViewModel
                {
                    PurchaseOrderNo =
                        GetSingleValue(
                            text,
                            @"Order\s*N[º°o]?\s*:\s*([0-9]+)"),

                    // Date of Order in the PDF.
                    PODate =
                        ParseDate(
                            GetSingleValue(
                                text,
                                @"Date\s*of\s*Order\s*:\s*([0-9.\/\-]+)")),

                    // Date of emission in the PDF is the date on which
                    // the PO was received by us.
                    ReceivedDate =
                        ParseDate(
                            GetSingleValue(
                                text,
                                @"Date\s*of\s*emission\s*:\s*([0-9.\/\-]+)")),

                    BuyerContactPerson =
                        GetBuyerName(text)
                };

            model.Items =
                ExtractItems(text);

            return model;
        }

        // ============================================================
        // FIXED MATERIAL ROW EXTRACTION
        // ============================================================

        private List<POImportItemViewModel> ExtractItems(
            string text)
        {
            text = NormalizePdfText(text);

            var items =
                new List<POImportItemViewModel>();

            /*
             Supported material row formats:

             N 10 15464033 2 UN 1531.00 UN 3062.00 31.07.26
             N 10 15464033 2 UN RS02 1531.00 UN 3062.00 31.07.26
             M 10 15464033 2 UN RS-02 1531.00 UN 3062.00 31.07.26
             C 10 15464033 2 UN RS02 1531.00 UN 3062.00 31.07.26
             10 15464033 2 UN RS02 1531.00 UN 3062.00 31.07.26

             Tax and other columns after the delivery date are allowed:

             N 10 15464033 2 UN 1531.00 UN 3062.00
             31.07.26 9% 275.58 9% 275.58
            */

            var rowPattern =
                @"^[ \t]*" +

                // Optional material status: N, M or C.
                @"(?:(?<status>N|M|C)[ \t]+)?" +

                // PO line-item number.
                @"(?<item>\d{1,4})[ \t]+" +

                // Material number is optional. Some WEG POs leave
                // the material column blank and continue directly with qty.
                @"(?:(?<material>\d{6,12})[ \t]+)?" +

                // Ordered quantity.
                @"(?<qty>[\d,]+(?:\.\d+)?)[ \t]+" +

                // Quantity unit.
                @"(?<unit>[A-Z]{1,5})[ \t]+" +

                // Optional department such as RS02 or RS-02.
                @"(?:(?<dep>RS-?\d{1,4})[ \t]+)?" +

                // Value/rate of goods.
                @"(?<value>[\d,]+(?:\.\d{1,2})?)[ \t]+" +

                // Price unit.
                @"(?<priceunit>[A-Z]{1,5})[ \t]+" +

                // Total material value.
                @"(?<total>[\d,]+(?:\.\d{1,2})?)[ \t]+" +

                // Delivery date.
                @"(?<delivery>\d{2}[.\/-]\d{2}[.\/-]\d{2,4})" +

                // Allow CGST, SGST and other values after delivery date.
                @"(?:[ \t]+[^\r\n]*)?[ \t]*$";

            var matches =
                Regex.Matches(
                    text,
                    rowPattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.Multiline);

            for (var index = 0;
                 index < matches.Count;
                 index++)
            {
                var match = matches[index];

                var startIndex =
                    match.Index;

                var endIndex =
                    index + 1 < matches.Count
                        ? matches[index + 1].Index
                        : text.Length;

                var itemBlockLength =
                    endIndex - startIndex;

                if (itemBlockLength <= 0)
                {
                    continue;
                }

                var itemBlock =
                    text.Substring(
                        startIndex,
                        itemBlockLength);

                var lineItemNumber =
                    match.Groups["item"]
                        .Value
                        .Trim();

                var materialNumber =
                    match.Groups["material"]
                        .Value
                        .Trim();

                if (string.IsNullOrWhiteSpace(
                        lineItemNumber))
                {
                    continue;
                }

                var fullDescription =
                    GetDescriptionBlock(itemBlock);

                var shortDescription =
                    GetShortDescription(
                        fullDescription);

                var drawingNo =
                    GetDrawingNo(itemBlock);

                var hsnSacCode =
                    GetHsnSacCode(itemBlock);

                var materialItem =
                    new POImportItemViewModel
                    {
                        // Preserve N/M/C status while keeping one
                        // consistent line-item format in the database.
                        LineItem =
                            (match.Groups["status"].Success
                                ? match.Groups["status"].Value
                                    .ToUpperInvariant()
                                : "N") + "-" + lineItemNumber,

                        MaterialNo =
                            materialNumber,

                        HsnSacCode =
                            hsnSacCode,

                        Quantity =
                            ParseDecimal(
                                match.Groups["qty"]
                                    .Value),

                        Unit =
                            match.Groups["unit"]
                                .Value
                                .Trim(),

                        Dep =
                            match.Groups["dep"].Success
                                ? match.Groups["dep"]
                                    .Value
                                    .Trim()
                                : "",

                        ValueOfGoods =
                            ParseDecimal(
                                match.Groups["value"]
                                    .Value),

                        PriceUnit =
                            match.Groups["priceunit"]
                                .Value
                                .Trim(),

                        TotalValue =
                            ParseDecimal(
                                match.Groups["total"]
                                    .Value),

                        DeliveryDate =
                            ParseDate(
                                match.Groups["delivery"]
                                    .Value),

                        ShortDescription =
                            shortDescription,

                        FullDescription =
                            fullDescription,

                        DrawingNo =
                            drawingNo
                    };

                items.Add(materialItem);
            }

            return items;
        }

        // ============================================================
        // EXTRACT FULL DESCRIPTION
        // ============================================================

        private string GetDescriptionBlock(
            string itemBlock)
        {
            var match =
                Regex.Match(
                    itemBlock,
                    @"Description\s*:\s*(?<desc>.*?)" +
                    @"(?:" +
                    @"\n\s*Drawing\s*:" +
                    @"|\n\s*HSN\s*/\s*SAC\s*Code\s*:" +
                    @"|\n\s*Receiver\s*:" +
                    @"|\n\s*Item\s+Material" +
                    @"|\n\s*Key\s*:" +
                    @"|\n\s*Total\s+value\s+of\s+order\s*:" +
                    @"|$" +
                    @")",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (!match.Success)
            {
                return "";
            }

            return CleanText(
                match.Groups["desc"].Value);
        }

        // ============================================================
        // SHORT DESCRIPTION
        // ============================================================

        private string GetShortDescription(
            string fullDescription)
        {
            if (string.IsNullOrWhiteSpace(
                    fullDescription))
            {
                return "";
            }

            var lines =
                fullDescription
                    .Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .ToList();

            return lines.FirstOrDefault() ?? "";
        }

        // ============================================================
        // DRAWING NUMBER
        // ============================================================

        private string GetDrawingNo(
            string itemBlock)
        {
            if (string.IsNullOrWhiteSpace(
                    itemBlock))
            {
                return "";
            }

            itemBlock =
                itemBlock.Replace(
                    "\r",
                    "\n");

            var lines =
                itemBlock
                    .Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .ToList();

            for (var index = 0;
                 index < lines.Count;
                 index++)
            {
                var line = lines[index];

                if (!line.Contains(
                        "Drawing",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var match =
                    Regex.Match(
                        line,
                        @"Drawing\s*:\s*(?<drawing>.*)$",
                        RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var drawingNo =
                        RemoveAfterStopWords(
                            match.Groups["drawing"]
                                .Value);

                    if (!string.IsNullOrWhiteSpace(
                            drawingNo))
                    {
                        return drawingNo;
                    }
                }

                if (index + 1 < lines.Count)
                {
                    var nextLine =
                        lines[index + 1].Trim();

                    if (!IsStopLine(nextLine))
                    {
                        return RemoveAfterStopWords(
                            nextLine);
                    }
                }
            }

            return "";
        }

        // ============================================================
        // HSN/SAC CODE
        // ============================================================

        private string GetHsnSacCode(
            string itemBlock)
        {
            if (string.IsNullOrWhiteSpace(
                    itemBlock))
            {
                return "";
            }

            var match =
                Regex.Match(
                    itemBlock,
                    @"HSN\s*/\s*SAC\s*Code\s*:\s*" +
                    @"(?<code>[0-9A-Za-z.\-\/]+)",
                    RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return "";
            }

            return CleanText(
                match.Groups["code"].Value);
        }

        // ============================================================
        // REMOVE TEXT AFTER DRAWING NUMBER
        // ============================================================

        private string RemoveAfterStopWords(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var stopWords = new[]
            {
                "HSN/SAC Code:",
                "HSN/SAC",
                "Receiver:",
                "Unloading Point:",
                "Item Material",
                "Description:",
                "Page:",
                "Key:",
                "Total value of order:",
                "Total Tax value:",
                "Currency:"
            };

            foreach (var stopWord in stopWords)
            {
                var stopWordIndex =
                    value.IndexOf(
                        stopWord,
                        StringComparison.OrdinalIgnoreCase);

                if (stopWordIndex >= 0)
                {
                    value =
                        value.Substring(
                            0,
                            stopWordIndex);
                }
            }

            return CleanText(value);
        }

        // ============================================================
        // CHECK STOP LINE
        // ============================================================

        private bool IsStopLine(
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            var stopWords = new[]
            {
                "HSN/SAC Code:",
                "HSN/SAC",
                "Receiver:",
                "Unloading Point:",
                "Item Material",
                "Description:",
                "Page:",
                "Key:",
                "Total value of order:",
                "Total Tax value:",
                "Currency:"
            };

            return stopWords.Any(x =>
                line.Contains(
                    x,
                    StringComparison.OrdinalIgnoreCase));
        }

        // ============================================================
        // BUYER NAME
        // ============================================================

        private string GetBuyerName(
            string text)
        {
            text = NormalizePdfText(text);

            // Most WEG PDFs print the buyer directly before
            // the Vendor label, often in a separate header column.
            var directMatch = Regex.Match(
                text,
                @"(?m)^[ \t]*(?<buyer>[A-Za-z][A-Za-z .]{1,60}?)[ \t]+Vendor\s*:",
                RegexOptions.IgnoreCase);

            if (directMatch.Success)
            {
                return CleanText(
                    directMatch.Groups["buyer"].Value);
            }

            var firstPattern =
                @"Order\s*N[º°o]?\s*:\s*\d+\s+" +
                @"(?<buyer>[A-Za-z][A-Za-z\s.]{1,60}?)\s+" +
                @"Vendor\s*:";

            var firstMatch =
                Regex.Match(
                    text,
                    firstPattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (firstMatch.Success)
            {
                return CleanText(
                    firstMatch.Groups["buyer"].Value);
            }

            var secondPattern =
                @"Quotation\s*:\s*(?:\n|\s)*" +
                @"(?<buyer>[A-Za-z][A-Za-z\s.]+?)\s*" +
                @"(?:\n|[A-Za-z0-9._%+\-]+@)";

            var secondMatch =
                Regex.Match(
                    text,
                    secondPattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (secondMatch.Success)
            {
                return CleanText(
                    secondMatch.Groups["buyer"].Value);
            }

            return "";
        }

        // ============================================================
        // GET SINGLE REGEX VALUE
        // ============================================================

        private string GetSingleValue(
            string text,
            string pattern)
        {
            var match =
                Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (match.Success &&
                match.Groups.Count > 1)
            {
                return CleanText(
                    match.Groups[1].Value);
            }

            return "";
        }

        // ============================================================
        // PARSE DATE
        // ============================================================

        private DateTime? ParseDate(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            var dateFormats = new[]
            {
                "dd.MM.yy",
                "dd.MM.yyyy",
                "dd/MM/yy",
                "dd/MM/yyyy",
                "dd-MM-yy",
                "dd-MM-yyyy"
            };

            if (DateTime.TryParseExact(
                    value,
                    dateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                return parsedDate;
            }

            return null;
        }

        // ============================================================
        // PARSE DECIMAL
        // ============================================================

        private decimal ParseDecimal(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            value =
                value.Replace(",", "")
                    .Trim();

            if (decimal.TryParse(
                    value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedValue))
            {
                return parsedValue;
            }

            return 0;
        }

        // ============================================================
        // NORMALIZE EXTRACTED PDF TEXT
        // ============================================================

        private string NormalizePdfText(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text =
                text.Replace(
                    "\r\n",
                    "\n");

            text =
                text.Replace(
                    "\r",
                    "\n");

            text =
                Regex.Replace(
                    text,
                    @"[ \t]+",
                    " ");

            text =
                Regex.Replace(
                    text,
                    @"\n[ \t]+",
                    "\n");

            text =
                Regex.Replace(
                    text,
                    @"\n{3,}",
                    "\n\n");

            return text.Trim();
        }

        // ============================================================
        // CLEAN EXTRACTED TEXT
        // ============================================================

        private string CleanText(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            value =
                value.Replace(
                    "\r\n",
                    "\n");

            value =
                value.Replace(
                    "\r",
                    "\n");

            value =
                Regex.Replace(
                    value,
                    @"[ \t]+",
                    " ");

            value =
                Regex.Replace(
                    value,
                    @"\n[ \t]+",
                    "\n");

            value =
                Regex.Replace(
                    value,
                    @"\n{2,}",
                    "\n");

            return value.Trim();
        }

        // ============================================================
        // INTERNAL PO SAVE RESULT
        // ============================================================

        private sealed class PurchaseOrderSaveResult
        {
            public bool WasCreated { get; set; }

            public int PurchaseOrderId { get; set; }

            public int MaterialRowsSaved { get; set; }

            public int MatchedInvoiceRows { get; set; }
        }

        // ============================================================
        // PDF TEXT LINE HELPER
        // ============================================================

        private class PdfTextLine
        {
            public double Top { get; set; }

            public List<(double Left, string Text)> Words
            {
                get;
                set;
            } = new();
        }
    }
}

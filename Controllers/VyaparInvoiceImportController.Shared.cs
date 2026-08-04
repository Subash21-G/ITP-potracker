using ClosedXML.Excel;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using POTracker.ViewModels;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace POTracker.Controllers
{
    public partial class VyaparInvoiceImportController : ControllerBase
    {
        private const int MaxBatchFileCount = 100;

        private const long MaxInvoiceFileSize =
            20L * 1024L * 1024L;

        private const long MaxBatchUploadSize =
            200L * 1024L * 1024L;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private readonly PrivateDocumentStorageService
            _privateDocumentStorage;

        private readonly PrivateDocumentReferenceService
            _documentReferences;

        public VyaparInvoiceImportController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            PrivateDocumentStorageService
                privateDocumentStorage,
            PrivateDocumentReferenceService
                documentReferences)
        {
            _context = context;
            _environment = environment;
            _privateDocumentStorage =
                privateDocumentStorage;
            _documentReferences =
                documentReferences;
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/purchase-order-crud")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class PurchaseOrderCrudApiController : ControllerBase
    {
        private const long MaximumPdfFileSize =
            50L * 1024L * 1024L;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private readonly PrivateDocumentStorageService
            _privateDocumentStorage;

        private readonly PrivateDocumentReferenceService
            _documentReferences;

        public PurchaseOrderCrudApiController(
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

        // ============================================================
        // DETAILS
        // ============================================================

        [HttpGet("{id:int}")]
        [ProducesResponseType(
            typeof(PurchaseOrderCrudDetailsDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<PurchaseOrderCrudDetailsDto>>
            Details(
                int id,
                CancellationToken cancellationToken = default)
        {
            var purchaseOrder =
                await GetDetailsAsync(
                    id,
                    cancellationToken);

            if (purchaseOrder == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Purchase order {id} was not found."
                    });
            }

            return Ok(purchaseOrder);
        }

        // ============================================================
        // PURCHASER OPTIONS
        // ============================================================

        [HttpGet("options/purchasers")]
        [ProducesResponseType(
            typeof(
                IReadOnlyList<
                    PurchaseOrderPurchaserOptionDto>),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<
                IReadOnlyList<
                    PurchaseOrderPurchaserOptionDto>>>
            GetPurchaserOptions(
                CancellationToken cancellationToken = default)
        {
            var options =
                await _context.Purchasers
                    .AsNoTracking()
                    .OrderBy(purchaser =>
                        purchaser.PurchaserName)
                    .Select(purchaser =>
                        new PurchaseOrderPurchaserOptionDto
                        {
                            Id = purchaser.Id,
                            PurchaserName =
                                purchaser.PurchaserName
                        })
                    .ToListAsync(
                        cancellationToken);

            return Ok(options);
        }

        // ============================================================
        // CREATE
        // ============================================================

        [HttpPost]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(PurchaseOrderSaveResponseDto),
            StatusCodes.Status201Created)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        public async Task<
            ActionResult<PurchaseOrderSaveResponseDto>>
            Create(
                [FromBody]
                PurchaseOrderUpsertRequestDto request,
                CancellationToken cancellationToken = default)
        {
            var purchaserExists =
                await _context.Purchasers
                    .AsNoTracking()
                    .AnyAsync(
                        purchaser =>
                            purchaser.Id ==
                            request.PurchaserId,
                        cancellationToken);

            if (!purchaserExists)
            {
                ModelState.AddModelError(
                    nameof(request.PurchaserId),
                    "The selected purchaser was not found.");
            }

            var poNumber =
                request.PONumber.Trim();

            var duplicateExists =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .AnyAsync(
                        purchaseOrder =>
                            purchaseOrder.PONumber ==
                            poNumber,
                        cancellationToken);

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    nameof(request.PONumber),
                    "This PO number already exists.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var purchaseOrder =
                new PurchaseOrder
                {
                    PurchaserId =
                        request.PurchaserId,

                    PONumber =
                        poNumber,

                    PODate =
                        request.PODate,

                    ReceivedDate =
                        request.ReceivedDate,

                    BuyerContactPerson =
                        Clean(
                            request.BuyerContactPerson),

                    CreatedDate =
                        DateTime.Now
                };

            _context.PurchaseOrders.Add(
                purchaseOrder);

            await _context.SaveChangesAsync(
                cancellationToken);

            var savedPurchaseOrder =
                await GetDetailsAsync(
                    purchaseOrder.Id,
                    cancellationToken);

            var response =
                new PurchaseOrderSaveResponseDto
                {
                    Success = true,
                    Message =
                        "Purchase order created successfully.",
                    PurchaseOrderId =
                        purchaseOrder.Id,
                    PurchaseOrder =
                        savedPurchaseOrder
                };

            return CreatedAtAction(
                nameof(Details),
                new
                {
                    id = purchaseOrder.Id
                },
                response);
        }

        // ============================================================
        // UPDATE
        // ============================================================

        [HttpPut("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(PurchaseOrderSaveResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<PurchaseOrderSaveResponseDto>>
            Update(
                int id,
                [FromBody]
                PurchaseOrderUpsertRequestDto request,
                CancellationToken cancellationToken = default)
        {
            var purchaseOrder =
                await _context.PurchaseOrders
                    .FirstOrDefaultAsync(
                        item => item.Id == id,
                        cancellationToken);

            if (purchaseOrder == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Purchase order {id} was not found."
                    });
            }

            var purchaserExists =
                await _context.Purchasers
                    .AsNoTracking()
                    .AnyAsync(
                        purchaser =>
                            purchaser.Id ==
                            request.PurchaserId,
                        cancellationToken);

            if (!purchaserExists)
            {
                ModelState.AddModelError(
                    nameof(request.PurchaserId),
                    "The selected purchaser was not found.");
            }

            var poNumber =
                request.PONumber.Trim();

            var duplicateExists =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .AnyAsync(
                        item =>
                            item.Id != id &&
                            item.PONumber == poNumber,
                        cancellationToken);

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    nameof(request.PONumber),
                    "This PO number already exists.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            purchaseOrder.PurchaserId =
                request.PurchaserId;

            purchaseOrder.PONumber =
                poNumber;

            purchaseOrder.PODate =
                request.PODate;

            purchaseOrder.ReceivedDate =
                request.ReceivedDate;

            purchaseOrder.BuyerContactPerson =
                Clean(
                    request.BuyerContactPerson);

            await _context.SaveChangesAsync(
                cancellationToken);

            var savedPurchaseOrder =
                await GetDetailsAsync(
                    purchaseOrder.Id,
                    cancellationToken);

            return Ok(
                new PurchaseOrderSaveResponseDto
                {
                    Success = true,
                    Message =
                        "Purchase order updated successfully.",
                    PurchaseOrderId =
                        purchaseOrder.Id,
                    PurchaseOrder =
                        savedPurchaseOrder
                });
        }

        // ============================================================
        // PDF UPLOAD
        // ============================================================

        [HttpPost("{id:int}/pdf")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(60L * 1024L * 1024L)]
        [ProducesResponseType(
            typeof(PurchaseOrderSaveResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<PurchaseOrderSaveResponseDto>>
            UploadPdf(
                int id,
                [FromForm]
                IFormFile? pdfFile,
                CancellationToken cancellationToken = default)
        {
            if (pdfFile == null ||
                pdfFile.Length == 0)
            {
                ModelState.AddModelError(
                    nameof(pdfFile),
                    "Select a PO PDF file.");

                return ValidationProblem(ModelState);
            }

            if (pdfFile.Length >
                MaximumPdfFileSize)
            {
                ModelState.AddModelError(
                    nameof(pdfFile),
                    "PO PDF size must not exceed 50 MB.");
            }

            var extension =
                Path.GetExtension(
                    pdfFile.FileName);

            if (!extension.Equals(
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(pdfFile),
                    "Only PDF files are allowed.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var purchaseOrder =
                await _context.PurchaseOrders
                    .FirstOrDefaultAsync(
                        item => item.Id == id,
                        cancellationToken);

            if (purchaseOrder == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Purchase order {id} was not found."
                    });
            }

            purchaseOrder.PdfFilePath =
                await SavePdfAsync(
                    pdfFile,
                    cancellationToken);

            await _context.SaveChangesAsync(
                cancellationToken);

            var savedPurchaseOrder =
                await GetDetailsAsync(
                    purchaseOrder.Id,
                    cancellationToken);

            return Ok(
                new PurchaseOrderSaveResponseDto
                {
                    Success = true,
                    Message =
                        "Purchase order PDF uploaded successfully.",
                    PurchaseOrderId =
                        purchaseOrder.Id,
                    PurchaseOrder =
                        savedPurchaseOrder
                });
        }

        // ============================================================
        // DELETE
        // ============================================================

        [HttpDelete("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(PurchaseOrderDeleteResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(PurchaseOrderDeleteResponseDto),
            StatusCodes.Status409Conflict)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<PurchaseOrderDeleteResponseDto>>
            Delete(
                int id,
                CancellationToken cancellationToken = default)
        {
            var purchaseOrder =
                await _context.PurchaseOrders
                    .FirstOrDefaultAsync(
                        item => item.Id == id,
                        cancellationToken);

            if (purchaseOrder == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Purchase order {id} was not found."
                    });
            }

            var materialCount =
                await _context.Materials
                    .AsNoTracking()
                    .CountAsync(
                        material =>
                            material.PurchaseOrderId == id,
                        cancellationToken);

            if (materialCount > 0)
            {
                return Conflict(
                    new PurchaseOrderDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Cannot delete this purchase order because material rows are linked.",
                        PurchaseOrderId = id
                    });
            }

            _context.PurchaseOrders.Remove(
                purchaseOrder);

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new PurchaseOrderDeleteResponseDto
                {
                    Success = true,
                    Message =
                        "Purchase order deleted successfully.",
                    PurchaseOrderId = id
                });
        }

        // ============================================================
        // DETAILS QUERY
        // ============================================================

        private async Task<PurchaseOrderCrudDetailsDto?>
            GetDetailsAsync(
                int id,
                CancellationToken cancellationToken)
        {
            var header =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .Where(purchaseOrder =>
                        purchaseOrder.Id == id)
                    .Select(purchaseOrder =>
                        new
                        {
                            purchaseOrder.Id,
                            purchaseOrder.PurchaserId,
                            PurchaserName =
                                purchaseOrder.Purchaser != null
                                    ? purchaseOrder.Purchaser
                                        .PurchaserName
                                    : string.Empty,
                            purchaseOrder.PONumber,
                            purchaseOrder.PODate,
                            purchaseOrder.ReceivedDate,
                            BuyerContactPerson =
                                purchaseOrder
                                    .BuyerContactPerson ??
                                string.Empty,
                            PdfFilePath =
                                purchaseOrder.PdfFilePath ??
                                string.Empty,
                            purchaseOrder.CreatedDate
                        })
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (header == null)
            {
                return null;
            }

            var summary =
                await _context.Materials
                    .AsNoTracking()
                    .Where(material =>
                        material.PurchaseOrderId == id)
                    .GroupBy(material => 1)
                    .Select(group =>
                        new
                        {
                            MaterialCount =
                                group.Count(),

                            TotalQuantity =
                                group.Sum(material =>
                                    material.Qty),

                            TotalInvoiceQuantity =
                                group.Sum(material =>
                                    material.InvoiceQty),

                            TotalDispatchQuantity =
                                group.Sum(material =>
                                    material.DispatchQty),

                            TotalPendingQuantity =
                                group.Sum(material =>
                                    material.PendingQty < 0
                                        ? 0
                                        : material.PendingQty),

                            TotalValue =
                                group.Sum(material =>
                                    material.TotalValue)
                        })
                    .FirstOrDefaultAsync(
                        cancellationToken);

            return new PurchaseOrderCrudDetailsDto
            {
                Id = header.Id,
                PurchaserId =
                    header.PurchaserId,
                PurchaserName =
                    header.PurchaserName,
                PONumber =
                    header.PONumber,
                PODate =
                    header.PODate,
                ReceivedDate =
                    header.ReceivedDate,
                BuyerContactPerson =
                    header.BuyerContactPerson,
                PdfFilePath =
                    _documentReferences.ToDownloadUrl(
                        header.PdfFilePath,
                        "po") ??
                    string.Empty,
                CreatedDate =
                    header.CreatedDate,
                MaterialCount =
                    summary?.MaterialCount ?? 0,
                TotalQuantity =
                    summary?.TotalQuantity ?? 0,
                TotalInvoiceQuantity =
                    summary?.TotalInvoiceQuantity ?? 0,
                TotalDispatchQuantity =
                    summary?.TotalDispatchQuantity ?? 0,
                TotalPendingQuantity =
                    summary?.TotalPendingQuantity ?? 0,
                TotalValue =
                    summary?.TotalValue ?? 0
            };
        }

        private async Task<string> SavePdfAsync(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var webRootPath =
                _environment.WebRootPath ??
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot");

            var uploadFolder =
                Path.Combine(
                    webRootPath,
                    "uploads",
                    "po");

            Directory.CreateDirectory(
                uploadFolder);

            var fileName =
                $"{Guid.NewGuid():N}.pdf";

            var publicPhysicalPath =
                Path.Combine(
                    uploadFolder,
                    fileName);

            StoredPrivateDocument privateDocument;

            try
            {
                await using (
                    var publicStream =
                        new FileStream(
                            publicPhysicalPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 81920,
                            useAsync: true))
                {
                    await file.CopyToAsync(
                        publicStream,
                        cancellationToken);
                }

                await using (
                    var privateSource =
                        new FileStream(
                            publicPhysicalPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            bufferSize: 81920,
                            useAsync: true))
                {
                    privateDocument =
                        await _privateDocumentStorage
                            .SaveWithStoredFileNameAsync(
                                privateSource,
                                "po",
                                fileName,
                                cancellationToken);
                }
            }
            catch
            {
                if (System.IO.File.Exists(
                        publicPhysicalPath))
                {
                    System.IO.File.Delete(
                        publicPhysicalPath);
                }

                throw;
            }

            var secureFilePath =
                _documentReferences.ToDownloadUrl(
                    privateDocument.RelativePath,
                    "po");

            if (secureFilePath == null)
            {
                try
                {
                    _privateDocumentStorage.DeleteIfExists(
                        privateDocument.RelativePath);
                }
                catch
                {
                    // Preserve the secure URL-generation failure.
                }

                if (System.IO.File.Exists(
                        publicPhysicalPath))
                {
                    System.IO.File.Delete(
                        publicPhysicalPath);
                }

                throw new InvalidOperationException(
                    "Unable to create a secure purchase order PDF URL.");
            }

            return secureFilePath;
        }

        private static string? Clean(
            string? value)
        {
            var cleaned = value?.Trim();

            return string.IsNullOrWhiteSpace(
                cleaned)
                ? null
                : cleaned;
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/dispatches")]
    public sealed class DispatchesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DispatchesApiController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // ============================================================
        // PENDING / PARTIAL DISPATCH MATERIAL LIST
        // ============================================================

        [HttpGet]
        public async Task<ActionResult<DispatchListResponseDto>> Index(
            string? search,
            string? status,
            int take = 500)
        {
            take = Math.Clamp(take, 10, 1000);

            var query = _context.Materials
                .AsNoTracking()
                .Where(material =>
                    material.Qty > material.DispatchQty ||
                    material.DispatchStatus == "Pending" ||
                    material.DispatchStatus == "Partial");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchValue = search.Trim();

                query = query.Where(material =>
                    (material.MaterialNo != null &&
                     material.MaterialNo.Contains(searchValue)) ||
                    (material.LineItem != null &&
                     material.LineItem.Contains(searchValue)) ||
                    (material.ShortDescription != null &&
                     material.ShortDescription.Contains(searchValue)) ||
                    (material.PurchaseOrder != null &&
                     material.PurchaseOrder.PONumber.Contains(searchValue)) ||
                    (material.PurchaseOrder != null &&
                     material.PurchaseOrder.Purchaser != null &&
                     material.PurchaseOrder.Purchaser.PurchaserName
                         .Contains(searchValue)));
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                !string.Equals(
                    status,
                    "All",
                    StringComparison.OrdinalIgnoreCase))
            {
                var statusValue = status.Trim();

                query = query.Where(material =>
                    material.DispatchStatus == statusValue);
            }

            var summary = new DispatchSummaryDto
            {
                TotalPendingMaterials = await query.CountAsync(),

                PartialMaterials = await query.CountAsync(material =>
                    material.DispatchQty > 0 &&
                    material.Qty > material.DispatchQty),

                TotalPOQuantity = await query.SumAsync(material =>
                    (decimal?)material.Qty) ?? 0m,

                TotalDispatchedQuantity = await query.SumAsync(material =>
                    (decimal?)material.DispatchQty) ?? 0m,

                TotalPendingQuantity = await query.SumAsync(material =>
                    (decimal?)(material.Qty > material.DispatchQty
                        ? material.Qty - material.DispatchQty
                        : 0m)) ?? 0m
            };

            var items = await query
                .OrderBy(material => material.DeliveryDate)
                .ThenBy(material =>
                    material.PurchaseOrder != null
                        ? material.PurchaseOrder.PONumber
                        : string.Empty)
                .ThenBy(material => material.LineItem)
                .Take(take)
                .Select(material =>
                    new DispatchMaterialListItemDto
                    {
                        MaterialId = material.Id,
                        PurchaseOrderId = material.PurchaseOrderId,

                        PONumber =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder.PONumber
                                : string.Empty,

                        PurchaserName =
                            material.PurchaseOrder != null &&
                            material.PurchaseOrder.Purchaser != null
                                ? material.PurchaseOrder.Purchaser
                                    .PurchaserName
                                : string.Empty,

                        LineItem =
                            material.LineItem ?? string.Empty,

                        MaterialNo =
                            material.MaterialNo ?? string.Empty,

                        Description =
                            material.ShortDescription ??
                            material.BriefDescription ??
                            string.Empty,

                        POQuantity = material.Qty,

                        DispatchQuantity =
                            material.DispatchQty,

                        PendingQuantity =
                            material.Qty > material.DispatchQty
                                ? material.Qty -
                                  material.DispatchQty
                                : 0m,

                        DeliveryDate =
                            material.DeliveryDate,

                        LastDispatchDate =
                            material.DispatchDate,

                        DispatchStatus =
                            material.DispatchQty <= 0
                                ? "Pending"
                                : material.DispatchQty >= material.Qty
                                    ? "Dispatched"
                                    : "Partial"
                    })
                .ToListAsync();

            return Ok(new DispatchListResponseDto
            {
                Items = items,
                Summary = summary
            });
        }

        // ============================================================
        // MATERIAL DISPATCH DETAILS AND HISTORY
        // ============================================================

        [HttpGet("{materialId:int}")]
        public async Task<ActionResult<DispatchDetailsResponseDto>>
            Details(
                int materialId)
        {
            if (materialId <= 0)
            {
                return BadRequest(new
                {
                    message = "Invalid material ID."
                });
            }

            var material = await _context.Materials
                .Include(item => item.PurchaseOrder)
                .ThenInclude(item => item!.Purchaser)
                .Include(item => item.Dispatches)
                .AsSplitQuery()
                .FirstOrDefaultAsync(item =>
                    item.Id == materialId);

            if (material == null)
            {
                return NotFound(new
                {
                    message = "Material was not found."
                });
            }

            var calculatedPendingQuantity = Math.Max(
                0m,
                material.Qty - material.DispatchQty);

            var calculatedStatus = GetDispatchStatus(
                material.Qty,
                material.DispatchQty,
                calculatedPendingQuantity);

            if (material.PendingQty !=
                    calculatedPendingQuantity ||
                !string.Equals(
                    material.DispatchStatus,
                    calculatedStatus,
                    StringComparison.Ordinal))
            {
                material.PendingQty =
                    calculatedPendingQuantity;

                material.DispatchStatus =
                    calculatedStatus;

                await _context.SaveChangesAsync();
            }

            return Ok(new DispatchDetailsResponseDto
            {
                Material = new DispatchMaterialDetailsDto
                {
                    MaterialId = material.Id,

                    PurchaseOrderId =
                        material.PurchaseOrderId,

                    PONumber =
                        material.PurchaseOrder?.PONumber ??
                        string.Empty,

                    PurchaserName =
                        material.PurchaseOrder?.Purchaser
                            ?.PurchaserName ??
                        string.Empty,

                    LineItem =
                        material.LineItem ?? string.Empty,

                    MaterialNo =
                        material.MaterialNo ?? string.Empty,

                    Description =
                        material.ShortDescription ??
                        material.BriefDescription ??
                        string.Empty,

                    POQuantity = material.Qty,

                    InvoiceQuantity =
                        material.InvoiceQty,

                    DispatchQuantity =
                        material.DispatchQty,

                    PendingQuantity =
                        calculatedPendingQuantity,

                    DeliveryDate =
                        material.DeliveryDate,

                    LastDispatchDate =
                        material.DispatchDate,

                    DispatchStatus =
                        calculatedStatus
                },

                Dispatches = material.Dispatches
                    .OrderByDescending(item =>
                        item.DispatchDate)
                    .ThenByDescending(item =>
                        item.CreatedDate)
                    .Select(item =>
                        new DispatchHistoryItemDto
                        {
                            Id = item.Id,
                            MaterialId = item.MaterialId,

                            DispatchNo =
                                item.DispatchNo,

                            DispatchDate =
                                item.DispatchDate,

                            DispatchQuantity =
                                item.DispatchQty,

                            Remarks =
                                item.Remarks ?? string.Empty,

                            FileName =
                                item.FileName ?? string.Empty,

                            FilePath =
                                item.FilePath ?? string.Empty,

                            CreatedDate =
                                item.CreatedDate
                        })
                    .ToList()
            });
        }

        // ============================================================
        // CREATE DISPATCH
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1024L * 1024L * 100L)]
        public async Task<ActionResult<DispatchMutationResponseDto>>
            Create(
                [FromForm] int materialId,
                [FromForm] string dispatchNo,
                [FromForm] DateTime? dispatchDate,
                [FromForm] decimal dispatchQuantity,
                [FromForm] string? remarks,
                [FromForm] IFormFile? dispatchFile)
        {
            if (materialId <= 0)
            {
                return BadRequest(
                    CreateFailureResponse(
                        "A valid material is required.",
                        materialId));
            }

            if (string.IsNullOrWhiteSpace(dispatchNo))
            {
                return BadRequest(
                    CreateFailureResponse(
                        "Dispatch No is required.",
                        materialId));
            }

            if (dispatchQuantity <= 0)
            {
                return BadRequest(
                    CreateFailureResponse(
                        "Dispatch quantity must be greater than zero.",
                        materialId));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var material = await _context.Materials
                    .FirstOrDefaultAsync(item =>
                        item.Id == materialId);

                if (material == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        CreateFailureResponse(
                            "Material was not found.",
                            materialId));
                }

                NormalizeDispatchBalance(material);

                if (material.PendingQty <= 0)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        CreateFailureResponse(
                            "This material is already fully dispatched.",
                            materialId,
                            material));
                }

                if (dispatchQuantity >
                    material.PendingQty)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        CreateFailureResponse(
                            $"Dispatch quantity cannot be greater than pending quantity {material.PendingQty:0.##}.",
                            materialId,
                            material));
                }

                var dispatch = new Dispatch
                {
                    MaterialId = material.Id,

                    DispatchNo =
                        dispatchNo.Trim(),

                    DispatchDate =
                        dispatchDate?.Date ??
                        DateTime.Today,

                    DispatchQty =
                        dispatchQuantity,

                    Remarks =
                        string.IsNullOrWhiteSpace(remarks)
                            ? null
                            : remarks.Trim(),

                    CreatedDate =
                        DateTime.Now
                };

                if (dispatchFile != null &&
                    dispatchFile.Length > 0)
                {
                    var savedFile =
                        await SaveFileAsync(
                            dispatchFile,
                            "dispatch");

                    dispatch.FileName =
                        savedFile.FileName;

                    dispatch.FilePath =
                        savedFile.FilePath;
                }

                _context.Dispatches.Add(dispatch);

                material.DispatchQty +=
                    dispatchQuantity;

                material.PendingQty = Math.Max(
                    0m,
                    material.Qty -
                    material.DispatchQty);

                material.DispatchDate =
                    dispatch.DispatchDate;

                material.DispatchStatus =
                    GetDispatchStatus(
                        material.Qty,
                        material.DispatchQty,
                        material.PendingQty);

                material.UpdatedDate =
                    DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new DispatchMutationResponseDto
                {
                    Success = true,

                    Message =
                        $"Dispatch {dispatch.DispatchNo} saved successfully.",

                    MaterialId =
                        material.Id,

                    DispatchId =
                        dispatch.Id,

                    DispatchQuantity =
                        material.DispatchQty,

                    PendingQuantity =
                        material.PendingQty,

                    DispatchStatus =
                        material.DispatchStatus
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    CreateFailureResponse(
                        "Unable to save dispatch. No partial changes were saved.",
                        materialId));
            }
        }

        // ============================================================
        // DELETE ONE DISPATCH
        // ============================================================

        [HttpDelete("{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<DispatchMutationResponseDto>>
            Delete(
                int id)
        {
            if (id <= 0)
            {
                return BadRequest(
                    CreateFailureResponse(
                        "Invalid dispatch ID.",
                        0));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var dispatch = await _context.Dispatches
                    .Include(item => item.Material)
                    .FirstOrDefaultAsync(item =>
                        item.Id == id);

                if (dispatch == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        CreateFailureResponse(
                            "Dispatch record was not found.",
                            0));
                }

                var material = dispatch.Material;

                if (material == null)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        CreateFailureResponse(
                            "The dispatch material was not found.",
                            dispatch.MaterialId));
                }

                material.DispatchQty = Math.Max(
                    0m,
                    material.DispatchQty -
                    dispatch.DispatchQty);

                material.PendingQty = Math.Max(
                    0m,
                    material.Qty -
                    material.DispatchQty);

                material.DispatchStatus =
                    GetDispatchStatus(
                        material.Qty,
                        material.DispatchQty,
                        material.PendingQty);

                material.DispatchDate =
                    material.DispatchQty <= 0
                        ? null
                        : await GetLatestDispatchDateAsync(
                            material.Id,
                            dispatch.Id);

                material.UpdatedDate =
                    DateTime.Now;

                _context.Dispatches.Remove(dispatch);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                DeletePhysicalFile(dispatch.FilePath);

                return Ok(new DispatchMutationResponseDto
                {
                    Success = true,

                    Message =
                        $"Dispatch {dispatch.DispatchNo} deleted and {dispatch.DispatchQty:0.##} quantity restored.",

                    MaterialId =
                        material.Id,

                    DispatchId =
                        dispatch.Id,

                    DispatchQuantity =
                        material.DispatchQty,

                    PendingQuantity =
                        material.PendingQty,

                    DispatchStatus =
                        material.DispatchStatus
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    CreateFailureResponse(
                        "Unable to delete dispatch. No partial changes were saved.",
                        0));
            }
        }

        // ============================================================
        // DELETE MULTIPLE DISPATCH RECORDS
        // ============================================================

        [HttpPost("bulk-delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<DispatchMutationResponseDto>>
            BulkDelete(
                [FromBody]
                DispatchBulkDeleteRequestDto request)
        {
            var dispatchIds = request.DispatchIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (request.MaterialId <= 0 ||
                dispatchIds.Count == 0)
            {
                return BadRequest(
                    CreateFailureResponse(
                        "Select at least one dispatch record.",
                        request.MaterialId));
            }

            if (dispatchIds.Count > 200)
            {
                return BadRequest(
                    CreateFailureResponse(
                        "A maximum of 200 dispatch records can be deleted at one time.",
                        request.MaterialId));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var material = await _context.Materials
                    .FirstOrDefaultAsync(item =>
                        item.Id == request.MaterialId);

                if (material == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        CreateFailureResponse(
                            "Material was not found.",
                            request.MaterialId));
                }

                var dispatches = await _context.Dispatches
                    .Where(item =>
                        item.MaterialId ==
                            request.MaterialId &&
                        dispatchIds.Contains(item.Id))
                    .ToListAsync();

                if (dispatches.Count == 0)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        CreateFailureResponse(
                            "Selected dispatch records were not found.",
                            request.MaterialId,
                            material));
                }

                var restoredQuantity =
                    dispatches.Sum(item =>
                        item.DispatchQty);

                var deletedIds = dispatches
                    .Select(item => item.Id)
                    .ToList();

                var filePaths = dispatches
                    .Select(item => item.FilePath)
                    .Where(path =>
                        !string.IsNullOrWhiteSpace(path))
                    .ToList();

                material.DispatchQty = Math.Max(
                    0m,
                    material.DispatchQty -
                    restoredQuantity);

                material.PendingQty = Math.Max(
                    0m,
                    material.Qty -
                    material.DispatchQty);

                material.DispatchStatus =
                    GetDispatchStatus(
                        material.Qty,
                        material.DispatchQty,
                        material.PendingQty);

                material.DispatchDate =
                    material.DispatchQty <= 0
                        ? null
                        : await _context.Dispatches
                            .Where(item =>
                                item.MaterialId ==
                                    material.Id &&
                                !deletedIds.Contains(item.Id))
                            .OrderByDescending(item =>
                                item.DispatchDate)
                            .Select(item =>
                                (DateTime?)item.DispatchDate)
                            .FirstOrDefaultAsync();

                material.UpdatedDate =
                    DateTime.Now;

                _context.Dispatches.RemoveRange(
                    dispatches);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                foreach (var filePath in filePaths)
                {
                    DeletePhysicalFile(filePath);
                }

                return Ok(new DispatchMutationResponseDto
                {
                    Success = true,

                    Message =
                        $"{dispatches.Count} dispatch record(s) deleted. {restoredQuantity:0.##} quantity restored.",

                    MaterialId =
                        material.Id,

                    DispatchQuantity =
                        material.DispatchQty,

                    PendingQuantity =
                        material.PendingQty,

                    DispatchStatus =
                        material.DispatchStatus
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    CreateFailureResponse(
                        "Unable to delete selected dispatch records. No partial changes were saved.",
                        request.MaterialId));
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static void NormalizeDispatchBalance(
            Material material)
        {
            material.DispatchQty = Math.Max(
                0m,
                material.DispatchQty);

            material.PendingQty = Math.Max(
                0m,
                material.Qty -
                material.DispatchQty);

            material.DispatchStatus =
                GetDispatchStatus(
                    material.Qty,
                    material.DispatchQty,
                    material.PendingQty);
        }

        private static string GetDispatchStatus(
            decimal poQuantity,
            decimal dispatchQuantity,
            decimal pendingQuantity)
        {
            if (dispatchQuantity <= 0)
            {
                return "Pending";
            }

            if (pendingQuantity <= 0 ||
                dispatchQuantity >= poQuantity)
            {
                return "Dispatched";
            }

            return "Partial";
        }

        private async Task<DateTime?>
            GetLatestDispatchDateAsync(
                int materialId,
                int excludedDispatchId)
        {
            return await _context.Dispatches
                .Where(item =>
                    item.MaterialId == materialId &&
                    item.Id != excludedDispatchId)
                .OrderByDescending(item =>
                    item.DispatchDate)
                .Select(item =>
                    (DateTime?)item.DispatchDate)
                .FirstOrDefaultAsync();
        }

        private static DispatchMutationResponseDto
            CreateFailureResponse(
                string message,
                int materialId,
                Material? material = null)
        {
            return new DispatchMutationResponseDto
            {
                Success = false,
                Message = message,
                MaterialId = materialId,

                DispatchQuantity =
                    material?.DispatchQty ?? 0m,

                PendingQuantity =
                    material?.PendingQty ?? 0m,

                DispatchStatus =
                    material?.DispatchStatus ??
                    string.Empty
            };
        }

        private async Task<(string FileName, string FilePath)>
            SaveFileAsync(
                IFormFile file,
                string folderName)
        {
            var webRootPath =
                _environment.WebRootPath ??
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot");

            var uploadFolder = Path.Combine(
                webRootPath,
                "uploads",
                folderName);

            Directory.CreateDirectory(
                uploadFolder);

            var originalFileName =
                Path.GetFileName(file.FileName);

            var storedFileName =
                $"{Guid.NewGuid()}_{originalFileName}";

            var physicalFilePath = Path.Combine(
                uploadFolder,
                storedFileName);

            await using var stream =
                new FileStream(
                    physicalFilePath,
                    FileMode.Create);

            await file.CopyToAsync(stream);

            return (
                originalFileName,
                $"/uploads/{folderName}/{storedFileName}");
        }

        private void DeletePhysicalFile(
            string? relativeFilePath)
        {
            if (string.IsNullOrWhiteSpace(
                    relativeFilePath))
            {
                return;
            }

            try
            {
                var webRootPath =
                    _environment.WebRootPath ??
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot");

                var normalizedRelativePath =
                    relativeFilePath
                        .TrimStart('/')
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar);

                var physicalFilePath =
                    Path.Combine(
                        webRootPath,
                        normalizedRelativePath);

                if (System.IO.File.Exists(
                        physicalFilePath))
                {
                    System.IO.File.Delete(
                        physicalFilePath);
                }
            }
            catch
            {
                // File cleanup failure must not roll back
                // the completed database transaction.
            }
        }
    }
}

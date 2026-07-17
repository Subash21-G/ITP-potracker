using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers
{
    public class DispatchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DispatchController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetString("UserName") != null;
        }

        // =========================
        // INDEX
        // =========================
        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var materials = await _context.Materials
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .Where(x =>
                    x.Qty > x.DispatchQty ||
                    x.DispatchStatus == "Pending" ||
                    x.DispatchStatus == "Partial")
                .OrderBy(x => x.DeliveryDate)
                .ToListAsync();

            return View(materials);
        }

        // =========================
        // CREATE GET
        // =========================
        [HttpGet]
        public async Task<IActionResult> Create(int materialId)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var material = await _context.Materials
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .FirstOrDefaultAsync(x => x.Id == materialId);

            if (material == null)
            {
                TempData["Error"] = "Material not found.";
                return RedirectToAction(nameof(Index));
            }

            NormalizeDispatchBalance(material);

            if (material.PendingQty <= 0)
            {
                TempData["Error"] = "This material is already fully dispatched.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Material = material;

            var dispatch = new Dispatch
            {
                MaterialId = material.Id,
                DispatchDate = DateTime.Today,
                DispatchQty = material.PendingQty
            };

            return View(dispatch);
        }

        // =========================
        // CREATE POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1024L * 1024L * 100L)]
        public async Task<IActionResult> Create(
            Dispatch dispatch,
            IFormFile? dispatchFile)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var material = await _context.Materials
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .FirstOrDefaultAsync(x => x.Id == dispatch.MaterialId);

            if (material == null)
            {
                TempData["Error"] = "Material not found.";
                return RedirectToAction(nameof(Index));
            }

            NormalizeDispatchBalance(material);

            if (string.IsNullOrWhiteSpace(dispatch.DispatchNo))
            {
                ModelState.AddModelError(
                    nameof(dispatch.DispatchNo),
                    "Dispatch No is required.");
            }

            if (dispatch.DispatchQty <= 0)
            {
                ModelState.AddModelError(
                    nameof(dispatch.DispatchQty),
                    "Dispatch qty must be greater than 0.");
            }

            if (dispatch.DispatchQty > material.PendingQty)
            {
                ModelState.AddModelError(
                    nameof(dispatch.DispatchQty),
                    $"Dispatch qty cannot be greater than pending qty {material.PendingQty}.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Material = material;
                return View(dispatch);
            }

            if (dispatchFile != null && dispatchFile.Length > 0)
            {
                dispatch.FilePath = await SaveFile(dispatchFile, "dispatch");
                dispatch.FileName = Path.GetFileName(dispatchFile.FileName);
            }

            dispatch.CreatedDate = DateTime.Now;

            _context.Dispatches.Add(dispatch);

            material.DispatchQty += dispatch.DispatchQty;
            material.PendingQty = material.Qty - material.DispatchQty;

            if (material.PendingQty < 0)
            {
                material.PendingQty = 0;
            }

            material.DispatchDate = dispatch.DispatchDate;

            material.DispatchStatus = GetDispatchStatus(
                material.Qty,
                material.DispatchQty,
                material.PendingQty);

            _context.Materials.Update(material);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Dispatch saved successfully.";

            return RedirectToAction(
                nameof(Details),
                new { materialId = material.Id });
        }

        // =========================
        // DETAILS
        // =========================
        [HttpGet]
        public async Task<IActionResult> Details(int materialId)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var material = await _context.Materials
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .Include(x => x.Dispatches)
                .FirstOrDefaultAsync(x => x.Id == materialId);

            if (material == null)
            {
                TempData["Error"] = "Material not found.";
                return RedirectToAction(nameof(Index));
            }

            NormalizeDispatchBalance(material);

            _context.Materials.Update(material);
            await _context.SaveChangesAsync();

            return View(material);
        }

        // =========================
        // DELETE DISPATCH
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var dispatch = await _context.Dispatches
                .Include(x => x.Material)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (dispatch == null)
            {
                TempData["Error"] = "Dispatch record not found.";
                return RedirectToAction(nameof(Index));
            }

            int? materialId = dispatch.MaterialId;
            var material = dispatch.Material;

            if (material != null)
            {
                material.DispatchQty -= dispatch.DispatchQty;

                if (material.DispatchQty < 0)
                {
                    material.DispatchQty = 0;
                }

                material.PendingQty = material.Qty - material.DispatchQty;

                if (material.PendingQty < 0)
                {
                    material.PendingQty = 0;
                }

                material.DispatchStatus = GetDispatchStatus(
                    material.Qty,
                    material.DispatchQty,
                    material.PendingQty);

                if (material.DispatchQty <= 0)
                {
                    material.DispatchDate = null;
                }

                _context.Materials.Update(material);
            }

            _context.Dispatches.Remove(dispatch);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Dispatch deleted successfully and material dispatch balance restored.";

            if (materialId.HasValue)
            {
                return RedirectToAction(
                    nameof(Details),
                    new { materialId = materialId.Value });
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // DELETE MULTIPLE DISPATCH RECORDS
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete(
            int materialId,
            List<int>? selectedIds)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var dispatchIds = (selectedIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (materialId <= 0 || dispatchIds.Count == 0)
            {
                TempData["Error"] =
                    "Please select at least one dispatch record.";

                return RedirectToAction(
                    nameof(Details),
                    new { materialId });
            }

            if (dispatchIds.Count > 200)
            {
                TempData["Error"] =
                    "A maximum of 200 dispatch records can be " +
                    "deleted at one time.";

                return RedirectToAction(
                    nameof(Details),
                    new { materialId });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var material = await _context.Materials
                    .FirstOrDefaultAsync(x => x.Id == materialId);

                if (material == null)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = "Material not found.";
                    return RedirectToAction(nameof(Index));
                }

                var dispatches = await _context.Dispatches
                    .Where(x =>
                        x.MaterialId == materialId &&
                        dispatchIds.Contains(x.Id))
                    .ToListAsync();

                if (dispatches.Count == 0)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] =
                        "The selected dispatch records were not found.";

                    return RedirectToAction(
                        nameof(Details),
                        new { materialId });
                }

                var quantityToRestore =
                    dispatches.Sum(x => x.DispatchQty);

                material.DispatchQty = Math.Max(
                    0,
                    material.DispatchQty - quantityToRestore);

                material.PendingQty = Math.Max(
                    0,
                    material.Qty - material.DispatchQty);

                material.DispatchStatus = GetDispatchStatus(
                    material.Qty,
                    material.DispatchQty,
                    material.PendingQty);

                if (material.DispatchQty <= 0)
                {
                    material.DispatchDate = null;
                }

                _context.Materials.Update(material);
                _context.Dispatches.RemoveRange(dispatches);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    $"{dispatches.Count} dispatch record(s) deleted. " +
                    $"{quantityToRestore:0.##} quantity was restored " +
                    "to pending dispatch.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] =
                    "Unable to delete the selected dispatch records. " +
                    "No partial changes were saved.";
            }

            return RedirectToAction(
                nameof(Details),
                new { materialId });
        }

        // =========================
        // NORMALIZE DISPATCH BALANCE
        // =========================
        private static void NormalizeDispatchBalance(Material material)
        {
            material.PendingQty = material.Qty - material.DispatchQty;

            if (material.PendingQty < 0)
            {
                material.PendingQty = 0;
            }

            material.DispatchStatus = GetDispatchStatus(
                material.Qty,
                material.DispatchQty,
                material.PendingQty);
        }

        // =========================
        // GET DISPATCH STATUS
        // =========================
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

        // =========================
        // SAVE FILE
        // =========================
        private async Task<string> SaveFile(
            IFormFile file,
            string folderName)
        {
            string webRootPath = _environment.WebRootPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var uploadFolder = Path.Combine(
                webRootPath,
                "uploads",
                folderName);

            Directory.CreateDirectory(uploadFolder);

            var safeFileName = Path.GetFileName(file.FileName);
            var fileName = $"{Guid.NewGuid()}_{safeFileName}";
            var filePath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{folderName}/{fileName}";
        }
    }
}

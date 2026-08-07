using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/purchasers")]
    [Produces("application/json")]
    public sealed class PurchasersApiController :
        ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PurchasersApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<
            ActionResult<PurchaserListResponseDto>>
            Index(
                string? search,
                CancellationToken cancellationToken)
        {
            var query =
                _context.Purchasers
                    .AsNoTracking()
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchValue =
                    search.Trim();

                query =
                    query.Where(item =>
                        item.PurchaserName
                            .Contains(searchValue) ||

                        (item.ContactPerson != null &&
                         item.ContactPerson
                            .Contains(searchValue)) ||

                        (item.MobileNo != null &&
                         item.MobileNo
                            .Contains(searchValue)) ||

                        (item.Email != null &&
                         item.Email
                            .Contains(searchValue)) ||

                        item.PurchaseOrders.Any(order =>
                            order.BuyerContactPerson != null &&
                            order.BuyerContactPerson
                                .Contains(searchValue)));
            }

            var items =
                await query
                    .OrderByDescending(item =>
                        item.Id)
                    .Select(item =>
                        new PurchaserListItemDto
                        {
                            Id =
                                item.Id,

                            PurchaserName =
                                item.PurchaserName,

                            ContactPerson =
                                item.ContactPerson ??
                                string.Empty,

                            MobileNo =
                                item.MobileNo ??
                                string.Empty,

                            Email =
                                item.Email ??
                                string.Empty,

                            Address =
                                item.Address ??
                                string.Empty,

                            PurchaseOrderCount =
                                _context.PurchaseOrders
                                    .Count(order =>
                                        order.PurchaserId ==
                                        item.Id),
                        })
                    .ToListAsync(
                        cancellationToken);

            var purchaserIds =
                items
                    .Select(item =>
                        item.Id)
                    .ToList();

            if (purchaserIds.Count > 0)
            {
                var personRows =
                    await _context.PurchaseOrders
                        .AsNoTracking()
                        .Where(order =>
                            purchaserIds.Contains(
                                order.PurchaserId) &&
                            order.BuyerContactPerson != null &&
                            order.BuyerContactPerson !=
                                string.Empty)
                        .Select(order =>
                            new
                            {
                                order.PurchaserId,

                                PersonName =
                                    order.BuyerContactPerson!
                            })
                        .ToListAsync(
                            cancellationToken);

                var peopleByPurchaser =
                    personRows
                        .Select(row =>
                            new
                            {
                                row.PurchaserId,

                                PersonName =
                                    row.PersonName.Trim()
                            })
                        .Where(row =>
                            !string.IsNullOrWhiteSpace(
                                row.PersonName))
                        .GroupBy(row =>
                            row.PurchaserId)
                        .ToDictionary(
                            group =>
                                group.Key,

                            group =>
                                group
                                    .GroupBy(
                                        row =>
                                            row.PersonName,
                                        StringComparer
                                            .OrdinalIgnoreCase)
                                    .Select(personGroup =>
                                        new
                                            PurchaserPurchaseOrderPersonDto
                                        {
                                            Name =
                                                personGroup.Key,

                                            PurchaseOrderCount =
                                                personGroup.Count()
                                        })
                                    .OrderByDescending(person =>
                                        person.PurchaseOrderCount)
                                    .ThenBy(person =>
                                        person.Name)
                                    .ToList());

                foreach (var item in items)
                {
                    if (peopleByPurchaser.TryGetValue(
                            item.Id,
                            out var people))
                    {
                        item.PurchaseOrderPersons =
                            people;
                    }
                }
            }

            return Ok(
                new PurchaserListResponseDto
                {
                    TotalCount =
                        items.Count,

                    Items =
                        items,
                });
        }

        [HttpGet("{id:int}")]
        public async Task<
            ActionResult<PurchaserDetailsDto>>
            Details(
                int id,
                CancellationToken cancellationToken)
        {
            var purchaser =
                await _context.Purchasers
                    .AsNoTracking()
                    .Where(item =>
                        item.Id == id)
                    .Select(item =>
                        new PurchaserDetailsDto
                        {
                            Id =
                                item.Id,

                            PurchaserName =
                                item.PurchaserName,

                            ContactPerson =
                                item.ContactPerson ??
                                string.Empty,

                            MobileNo =
                                item.MobileNo ??
                                string.Empty,

                            Email =
                                item.Email ??
                                string.Empty,

                            Address =
                                item.Address ??
                                string.Empty,

                            PurchaseOrderCount =
                                _context.PurchaseOrders
                                    .Count(order =>
                                        order.PurchaserId ==
                                        item.Id),
                        })
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (purchaser == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            "Purchaser was not found."
                    });
            }

            return Ok(purchaser);
        }

        [HttpPost]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<
            ActionResult<PurchaserSaveResponseDto>>
            Create(
                [FromBody]
                PurchaserSaveRequestDto request,
                CancellationToken cancellationToken)
        {
            var purchaserName =
                request.PurchaserName.Trim();

            if (string.IsNullOrWhiteSpace(
                    purchaserName))
            {
                ModelState.AddModelError(
                    nameof(request.PurchaserName),
                    "Purchaser Name is required.");

                return ValidationProblem(
                    ModelState);
            }

            var duplicateExists =
                await _context.Purchasers
                    .AsNoTracking()
                    .AnyAsync(
                        item =>
                            item.PurchaserName ==
                            purchaserName,
                        cancellationToken);

            if (duplicateExists)
            {
                return Conflict(
                    new
                    {
                        error =
                            "A purchaser with this name already exists."
                    });
            }

            var purchaser =
                new Purchaser
                {
                    PurchaserName =
                        purchaserName,

                    ContactPerson =
                        Clean(request.ContactPerson),

                    MobileNo =
                        Clean(request.MobileNo),

                    Email =
                        Clean(request.Email),

                    Address =
                        Clean(request.Address),
                };

            _context.Purchasers.Add(
                purchaser);

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new PurchaserSaveResponseDto
                {
                    Success =
                        true,

                    Message =
                        "Purchaser created successfully.",

                    PurchaserId =
                        purchaser.Id,
                });
        }

        [HttpPut("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<
            ActionResult<PurchaserSaveResponseDto>>
            Update(
                int id,
                [FromBody]
                PurchaserSaveRequestDto request,
                CancellationToken cancellationToken)
        {
            var purchaser =
                await _context.Purchasers
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (purchaser == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            "Purchaser was not found."
                    });
            }

            var purchaserName =
                request.PurchaserName.Trim();

            if (string.IsNullOrWhiteSpace(
                    purchaserName))
            {
                ModelState.AddModelError(
                    nameof(request.PurchaserName),
                    "Purchaser Name is required.");

                return ValidationProblem(
                    ModelState);
            }

            var duplicateExists =
                await _context.Purchasers
                    .AsNoTracking()
                    .AnyAsync(
                        item =>
                            item.Id != id &&
                            item.PurchaserName ==
                            purchaserName,
                        cancellationToken);

            if (duplicateExists)
            {
                return Conflict(
                    new
                    {
                        error =
                            "Another purchaser with this name already exists."
                    });
            }

            purchaser.PurchaserName =
                purchaserName;

            purchaser.ContactPerson =
                Clean(request.ContactPerson);

            purchaser.MobileNo =
                Clean(request.MobileNo);

            purchaser.Email =
                Clean(request.Email);

            purchaser.Address =
                Clean(request.Address);

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new PurchaserSaveResponseDto
                {
                    Success =
                        true,

                    Message =
                        "Purchaser updated successfully.",

                    PurchaserId =
                        purchaser.Id,
                });
        }

        [HttpDelete("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<
            ActionResult<PurchaserDeleteResponseDto>>
            Delete(
                int id,
                CancellationToken cancellationToken)
        {
            var purchaser =
                await _context.Purchasers
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (purchaser == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            "Purchaser was not found."
                    });
            }

            var purchaseOrderCount =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .CountAsync(
                        order =>
                            order.PurchaserId ==
                            id,
                        cancellationToken);

            if (purchaseOrderCount > 0)
            {
                return Conflict(
                    new
                    {
                        error =
                            $"This purchaser cannot be deleted because it is linked to {purchaseOrderCount} Purchase Order record(s)."
                    });
            }

            _context.Purchasers.Remove(
                purchaser);

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new PurchaserDeleteResponseDto
                {
                    Success =
                        true,

                    Message =
                        "Purchaser deleted successfully.",

                    PurchaserId =
                        id,
                });
        }

        private static string? Clean(
            string? value)
        {
            return string.IsNullOrWhiteSpace(
                    value)
                ? null
                : value.Trim();
        }
    }
}

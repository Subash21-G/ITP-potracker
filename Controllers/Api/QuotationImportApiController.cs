using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Security;

namespace POTracker.Controllers.Api;

[ApiController]
[Route("api/purchase-orders")]
public sealed class QuotationImportApiController(ApplicationDbContext database, IConfiguration configuration) : ControllerBase
{
    [HttpPost("from-quotation")]
    [QuotationIntegration]
    public async Task<IActionResult> Import(QuotationImportRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var quotation = request.QuotationNumber.Trim();
        var poNumber = request.CustomerPoNumber.Trim();
        var customer = request.CustomerCode.Trim();
        if (!int.TryParse(configuration[$"QuotationIntegration:CustomerMappings:{customer}"], out var purchaserId) ||
            !await database.Purchasers.AnyAsync(x => x.Id == purchaserId, cancellationToken))
            return BadRequest(new { error = "Customer code is not mapped to an existing POTracker purchaser." });

        // Serializable isolation plus unique indexes protects simultaneous deliveries.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await database.PurchaseOrders.Include(x => x.Materials).FirstOrDefaultAsync(x =>
            x.SourceSystem == "ITPQuotation" && x.QuotationNumber == quotation && x.QuotationRevision == request.Revision,
            cancellationToken);
        if (existing != null)
        {
            var rows = existing.Materials.OrderBy(x => x.Id).ToList();
            var matches = existing.PONumber == poNumber && existing.CustomerCode == customer &&
                existing.RfqNumber == request.RfqNumber?.Trim() && rows.Count == request.Items.Count &&
                rows.Zip(request.Items).All(pair => pair.First.MaterialNo == pair.Second.MaterialNo.Trim() &&
                    pair.First.ShortDescription == pair.Second.Description.Trim() && pair.First.Qty == pair.Second.Quantity &&
                    pair.First.Cost == pair.Second.UnitPrice && pair.First.TotalValue == pair.Second.TotalPrice);
            if (!matches) return Conflict(new { error = "This quotation revision was already imported with different data." });
            return Ok(new { purchaseOrderId = existing.Id, poNumber = existing.PONumber, quotationNumber = quotation, revision = request.Revision, alreadyImported = true });
        }
        if (await database.PurchaseOrders.AnyAsync(x => x.PONumber == poNumber, cancellationToken))
            return Conflict(new { error = "This customer PO number already exists. Revised quotations require explicit reconciliation; existing production rows are preserved." });
        var total = request.Items.Sum(x => x.TotalPrice);
        if (total > 9999999999999999.99m) return BadRequest(new { error = "Order total exceeds the supported amount." });
        var order = new PurchaseOrder
        {
            PurchaserId = purchaserId, PONumber = poNumber, SourceSystem = "ITPQuotation",
            QuotationNumber = quotation, QuotationRevision = request.Revision,
            RfqNumber = request.RfqNumber?.Trim(), CustomerCode = customer,
            ImportFormat = "ITPQuotation", BasicValue = total, GrandTotal = total,
            Materials = request.Items.Select((item, index) => new Material
            {
                LineItem = ((index + 1) * 10).ToString(), MaterialNo = item.MaterialNo.Trim(),
                ShortDescription = item.Description.Trim(), Qty = item.Quantity,
                PendingQty = item.Quantity, BalanceQty = item.Quantity, Cost = item.UnitPrice,
                TotalValue = item.TotalPrice, ValueOfGoods = item.TotalPrice, TotalValueWithTax = item.TotalPrice
            }).ToList()
        };
        database.PurchaseOrders.Add(order);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Created($"/api/purchase-order-crud/{order.Id}", new
        {
            purchaseOrderId = order.Id, poNumber, quotationNumber = quotation, revision = request.Revision, alreadyImported = false
        });
    }
}

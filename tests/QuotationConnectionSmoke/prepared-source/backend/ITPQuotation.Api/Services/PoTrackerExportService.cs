using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ITPQuotation.Api.Data;
using ITPQuotation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ITPQuotation.Api.Services;

public sealed class PoTrackerExportException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed record PoTrackerExportResult(int PurchaseOrderId, string PoNumber, string QuotationNumber, int Revision, bool AlreadyImported);

public sealed class PoTrackerExportService(ApplicationDbContext database, IConfiguration configuration, HttpClient http)
{
    public async Task<PoTrackerExportResult?> ExportAsync(int id, string customerPoNumber, CancellationToken cancellationToken)
    {
        var quotation = await database.Quotations.Include(x => x.Rfq).Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (quotation is null) return null;
        if (quotation.Status != QuotationValues.Accepted && quotation.Status != QuotationValues.PoReceived)
            throw new PoTrackerExportException(400, "Only accepted quotations or quotations with a received customer PO can be sent.");
        if (string.IsNullOrWhiteSpace(customerPoNumber) || customerPoNumber.Trim().Length > 100)
            throw new PoTrackerExportException(400, "Enter the confirmed customer PO number (up to 100 characters).");

        var key = configuration["POTracker:ApiKey"];
        var customerCode = configuration[$"POTracker:CustomerCodes:{quotation.CustomerId}"];
        if (!Uri.TryCreate(configuration["POTracker:BaseUrl"], UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != "https" && !(baseUri.Scheme == "http" && baseUri.IsLoopback))
            || !string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrWhiteSpace(baseUri.Query)
            || !string.IsNullOrWhiteSpace(baseUri.Fragment) || string.IsNullOrWhiteSpace(key))
            throw new PoTrackerExportException(503, "POTracker connection is not configured. Configure its URL and shared API key in the backend.");
        if (string.IsNullOrWhiteSpace(customerCode))
            throw new PoTrackerExportException(400, "This customer has no POTracker customer-code mapping. Ask the administrator to configure it.");
        if (quotation.Items.Count == 0 || quotation.Items.Any(item => item.Quantity <= 0
            || item.Quantity != decimal.Round(item.Quantity, 2) || item.UnitPrice < 0
            || string.IsNullOrWhiteSpace(item.MaterialNo) || string.IsNullOrWhiteSpace(item.Description)))
            throw new PoTrackerExportException(400, "Every item needs a material number, description, positive quantity with at most two decimal places, and a nonnegative price.");

        var revision = checked(quotation.Revision + 1);
        var poNumber = customerPoNumber.Trim();
        var payload = new
        {
            sourceSystem = "ITPQuotation", quotationNumber = quotation.QuotationNumber, revision,
            rfqNumber = quotation.Rfq.RfqNumber, customerPoNumber = poNumber, customerCode = customerCode.Trim(),
            items = quotation.Items.OrderBy(x => x.Id).Select(item => new
            {
                materialNo = item.MaterialNo, description = item.Description, quantity = item.Quantity,
                unitPrice = decimal.Round(item.UnitPrice, 2, MidpointRounding.AwayFromZero),
                totalPrice = decimal.Round(item.Quantity * decimal.Round(item.UnitPrice, 2, MidpointRounding.AwayFromZero), 2, MidpointRounding.AwayFromZero)
            }).ToArray()
        };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/api/purchase-orders/from-quotation"));
        request.Headers.Add("X-Quotation-Api-Key", key);
        request.Content = JsonContent.Create(payload);
        try
        {
            // A repeated delivery is safe: POTracker deduplicates by quotation and revision.
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "POTracker rejected the shared API key. Ask the administrator to check both backends.",
                    HttpStatusCode.Conflict => "POTracker already has this quotation revision or PO with different data. Reconcile the existing order before retrying.",
                    HttpStatusCode.BadRequest => "POTracker rejected the customer mapping or item data. Check the mapping and order details.",
                    _ => "POTracker is unavailable. Retry this export later with the same customer PO number."
                };
                throw new PoTrackerExportException(response.StatusCode == HttpStatusCode.Conflict ? 409
                    : response.StatusCode == HttpStatusCode.BadRequest ? 400 : 502, message);
            }
            var result = await response.Content.ReadFromJsonAsync<PoTrackerExportResult>(cancellationToken);
            if (result is null || result.PurchaseOrderId <= 0 || result.PoNumber != poNumber
                || result.QuotationNumber != quotation.QuotationNumber || result.Revision != revision)
                throw new PoTrackerExportException(502, "POTracker returned an unexpected response. Retry with the same PO number.");
            quotation.PoTrackerPurchaseOrderId = result.PurchaseOrderId;
            quotation.PoTrackerPoNumber = result.PoNumber;
            quotation.PoTrackerExportedRevision = quotation.Revision;
            await database.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (HttpRequestException)
        {
            throw new PoTrackerExportException(502, "Unable to reach POTracker. Retry later with the same customer PO number.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PoTrackerExportException(504, "POTracker timed out. Retry with the same customer PO number; duplicate orders will be avoided.");
        }
        catch (JsonException)
        {
            throw new PoTrackerExportException(502, "POTracker returned an unreadable response. Retry with the same customer PO number.");
        }
    }
}

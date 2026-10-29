using System.ComponentModel.DataAnnotations;

namespace POTracker.Contracts.Api;

public sealed class QuotationImportRequest : IValidatableObject
{
    [Required, StringLength(100)] public string SourceSystem { get; set; } = "ITPQuotation";
    [Required, StringLength(100)] public string QuotationNumber { get; set; } = "";
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [StringLength(100)] public string? RfqNumber { get; set; }
    [Required, StringLength(100)] public string CustomerPoNumber { get; set; } = "";
    [Required, StringLength(100)] public string CustomerCode { get; set; } = "";
    [Required, MinLength(1), MaxLength(1000)] public List<QuotationImportItem> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SourceSystem.Trim() != "ITPQuotation")
            yield return new ValidationResult("SourceSystem must be ITPQuotation.", new[] { nameof(SourceSystem) });
        if (Items == null) yield break;
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            if (item == null)
            {
                yield return new ValidationResult("Item cannot be null.", new[] { $"Items[{index}]" });
                continue;
            }
            if (item.Quantity != decimal.Round(item.Quantity, 2) || item.UnitPrice != decimal.Round(item.UnitPrice, 2) ||
                item.TotalPrice != decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero))
                yield return new ValidationResult("Use at most two decimal places; totalPrice must equal quantity × unitPrice rounded to two places.", new[] { $"Items[{index}]" });
        }
    }
}

public sealed class QuotationImportItem
{
    [Required, StringLength(150)] public string MaterialNo { get; set; } = "";
    [Required, StringLength(500)] public string Description { get; set; } = "";
    [Range(typeof(decimal), "0.01", "9999999999999")] public decimal Quantity { get; set; }
    [Range(typeof(decimal), "0", "9999999999999")] public decimal UnitPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal TotalPrice { get; set; }
}

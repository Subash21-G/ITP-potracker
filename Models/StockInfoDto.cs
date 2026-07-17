namespace POTracker.Models
{
    public class StockInfoDto
    {
        public bool Success { get; set; }

        public int Id { get; set; }

        public string? MaterialCode { get; set; }

        public string? MaterialNo { get; set; }

        public string? Description { get; set; }

        public string? SupplierName { get; set; }

        public string? Location { get; set; }

        public decimal CurrentStock { get; set; }

        public decimal MinimumStock { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalValue { get; set; }

        public bool IsLowStock { get; set; }

        public string? Status { get; set; }

        public DateTime? LastUpdated { get; set; }
    }
}
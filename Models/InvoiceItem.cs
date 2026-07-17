using System.ComponentModel.DataAnnotations.Schema;

namespace POTracker.Models
{
    public class InvoiceItem
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public int MaterialId { get; set; }
        public Material? Material { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? HsnSacCode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InvoiceQty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceUnit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GstAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
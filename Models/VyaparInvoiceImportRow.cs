using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POTracker.Models
{
    public class VyaparInvoiceImportRow
    {
        [Key]
        public int Id { get; set; }

        public int BatchId { get; set; }

        public VyaparInvoiceImportBatch? Batch { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal InvoiceQty { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
    }
}
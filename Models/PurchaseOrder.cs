using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POTracker.Models
{
    public class PurchaseOrder
    {
        public int Id { get; set; }

        [StringLength(100)]
        public string? SourceSystem { get; set; }

        [StringLength(100)]
        public string? QuotationNumber { get; set; }

        public int? QuotationRevision { get; set; }

        [StringLength(100)]
        public string? RfqNumber { get; set; }

        [StringLength(100)]
        public string? CustomerCode { get; set; }

        // Existing Purchaser relationship
        public int PurchaserId { get; set; }

        public Purchaser? Purchaser { get; set; }

        // =========================
        // PO BASIC DETAILS
        // =========================

        [Required]
        [StringLength(100)]
        public string PONumber { get; set; } = string.Empty;

        // Date of Order from PDF
        public DateTime? PODate { get; set; }

        // Date of Emission from PDF
        public DateTime? ReceivedDate { get; set; }

        // Buyer / Contact Person from PDF
        [StringLength(200)]
        public string? BuyerContactPerson { get; set; }

        // Buyer email from PDF
        [StringLength(250)]
        [EmailAddress]
        public string? BuyerEmail { get; set; }

        // =========================
        // VENDOR DETAILS
        // =========================

        [StringLength(100)]
        public string? VendorCode { get; set; }

        [StringLength(250)]
        public string? VendorName { get; set; }

        // =========================
        // COMMERCIAL DETAILS
        // =========================

        [StringLength(250)]
        public string? PaymentTerms { get; set; }

        [StringLength(250)]
        public string? DeliveryCondition { get; set; }

        [StringLength(50)]
        public string? Currency { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BasicValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; }

        // =========================
        // IMPORT DETAILS
        // =========================

        // Example: WEG, Standard, Manual
        [StringLength(100)]
        public string? ImportFormat { get; set; }

        // Uploaded PO PDF path
        [StringLength(500)]
        public string? PdfFilePath { get; set; }

        // Open / Partial / Completed / Cancelled
        [StringLength(50)]
        public string Status { get; set; } = "Open";

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; }

        // =========================
        // RELATIONSHIPS
        // =========================

        public ICollection<Material> Materials { get; set; }
            = new List<Material>();
    }
}

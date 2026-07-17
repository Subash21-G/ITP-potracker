using System.ComponentModel.DataAnnotations;

namespace POTracker.Models
{
    public class Invoice
    {
        public int Id { get; set; }

        [Required]
        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime? InvoiceDate { get; set; }

        public string? CustomerName { get; set; }

        public string? UploadedFileName { get; set; }

        public string? UploadedFilePath { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    }
}
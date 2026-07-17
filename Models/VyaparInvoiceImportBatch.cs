using System.ComponentModel.DataAnnotations;

namespace POTracker.Models
{
    public class VyaparInvoiceImportBatch
    {
        [Key]
        public int Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public DateTime ImportDate { get; set; } = DateTime.Now;

        public int TotalRows { get; set; }

        public int SuccessRows { get; set; }

        public int FailedRows { get; set; }

        public ICollection<VyaparInvoiceImportRow> Rows { get; set; } = new List<VyaparInvoiceImportRow>();
    }
}
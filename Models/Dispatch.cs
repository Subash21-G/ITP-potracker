using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POTracker.Models
{
    public class Dispatch
    {
        public int Id { get; set; }

        public int MaterialId { get; set; }
        public Material? Material { get; set; }

        [Required]
        public string DispatchNo { get; set; } = string.Empty;

        public DateTime DispatchDate { get; set; } = DateTime.Today;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DispatchQty { get; set; }

        public string? Remarks { get; set; }

        public string? FileName { get; set; }

        public string? FilePath { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
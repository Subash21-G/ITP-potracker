using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POTracker.Models
{
    public class Stock
    {
        public int Id { get; set; }

        [Required]
        public string MaterialNo { get; set; } = string.Empty;

        public string? MaterialName { get; set; }

        public string? RMGrade { get; set; }

        public string? Location { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AvailableQty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumQty { get; set; }

        public DateTime UpdatedDate { get; set; } = DateTime.Now;
    }
}
using System;
using System.ComponentModel.DataAnnotations;

namespace POTracker.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public int? MaterialId { get; set; }

        public Material? Material { get; set; }

        [StringLength(100)]
        public string? MaterialNo { get; set; }

        [StringLength(100)]
        public string? PONumber { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ProcessStatus { get; set; }

        [StringLength(50)]
        public string? DispatchStatus { get; set; }

        [StringLength(100)]
        public string? Owner { get; set; }

        [StringLength(100)]
        public string? UserName { get; set; }

        [StringLength(1000)]
        public string? Details { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}


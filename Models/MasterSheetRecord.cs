using System.ComponentModel.DataAnnotations;

namespace POTracker.Models
{
    public sealed class MasterSheetRecord
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string MaterialNo { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ShortDescription { get; set; }

        [StringLength(200)]
        public string? Grade { get; set; }

        [StringLength(200)]
        public string? DrawingNo { get; set; }

        [StringLength(100)]
        public string? DrawingVersion { get; set; }

        [StringLength(200)]
        public string? FinishSize { get; set; }

        [StringLength(200)]
        public string? RequiredRmSize { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; }

        public ICollection<MasterSheetProcessStep> ProcessSteps { get; set; }
            = new List<MasterSheetProcessStep>();
    }

    public sealed class MasterSheetProcessStep
    {
        public int Id { get; set; }

        public int MasterSheetRecordId { get; set; }

        public MasterSheetRecord? MasterSheetRecord { get; set; }

        public int Sequence { get; set; }

        [Required]
        [StringLength(200)]
        public string ProcessName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? VendorName { get; set; }

        [Required]
        [StringLength(20)]
        public string Source { get; set; } = "In-house";
    }

    public sealed class MasterSheetProcessOption
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string ProcessName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? VendorName { get; set; }

        [Required]
        [StringLength(20)]
        public string Source { get; set; } = "In-house";

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}

using System.ComponentModel.DataAnnotations;

namespace POTracker.Contracts.Api
{
    public sealed class MasterSheetProcessStepDto
    {
        public int Id { get; init; }

        public int Sequence { get; init; }

        public string ProcessName { get; init; } = string.Empty;

        public string VendorName { get; init; } = string.Empty;

        public string Source { get; init; } = "In-house";
    }

    public sealed class MasterSheetRecordDto
    {
        public int Id { get; init; }

        public string MaterialNo { get; init; } = string.Empty;

        public string ShortDescription { get; init; } = string.Empty;

        public string Grade { get; init; } = string.Empty;

        public string DrawingNo { get; init; } = string.Empty;
        public string DrawingVersion { get; init; } = string.Empty;

        public string FinishSize { get; init; } = string.Empty;

        public string RequiredRmSize { get; init; } = string.Empty;

        public IReadOnlyList<MasterSheetProcessStepDto> ProcessSteps { get; init; }
            = Array.Empty<MasterSheetProcessStepDto>();

        public DateTime CreatedDate { get; init; }

        public DateTime? UpdatedDate { get; init; }
    }

    public sealed class MasterSheetProcessStepRequestDto
    {
        [Required]
        [StringLength(200)]
        public string ProcessName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? VendorName { get; set; }

        [Required]
        [RegularExpression(
            "^(In-house|Outsource)$",
            ErrorMessage = "Source must be In-house or Outsource.")]
        public string Source { get; set; } = "In-house";
    }

    public sealed class MasterSheetSaveRequestDto
    {
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

        [Required]
        public List<MasterSheetProcessStepRequestDto> ProcessSteps { get; set; }
            = new();
    }

    public sealed class MasterSheetProcessOptionDto
    {
        public int Id { get; init; }

        public string ProcessName { get; init; } = string.Empty;

        public string VendorName { get; init; } = string.Empty;

        public string Source { get; init; } = "In-house";
    }

    public sealed class MasterSheetProcessOptionRequestDto
    {
        [Required]
        [StringLength(200)]
        public string ProcessName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? VendorName { get; set; }

        [Required]
        [RegularExpression(
            "^(In-house|Outsource)$",
            ErrorMessage = "Source must be In-house or Outsource.")]
        public string Source { get; set; } = "In-house";
    }

    public sealed class MasterSheetDeleteResponseDto
    {
        public bool Success { get; init; }

        public string MaterialNo { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }
}

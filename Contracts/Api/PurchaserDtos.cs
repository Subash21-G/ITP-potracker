using System.ComponentModel.DataAnnotations;

namespace POTracker.Contracts.Api
{
    public sealed class PurchaserListItemDto
    {
        public int Id { get; set; }

        public string PurchaserName { get; set; } =
            string.Empty;

        public string ContactPerson { get; set; } =
            string.Empty;

        public string MobileNo { get; set; } =
            string.Empty;

        public string Email { get; set; } =
            string.Empty;

        public string Address { get; set; } =
            string.Empty;

        public int PurchaseOrderCount { get; set; }
    }

    public sealed class PurchaserListResponseDto
    {
        public int TotalCount { get; set; }

        public List<PurchaserListItemDto> Items { get; set; } =
            new();
    }

    public sealed class PurchaserDetailsDto
    {
        public int Id { get; set; }

        public string PurchaserName { get; set; } =
            string.Empty;

        public string ContactPerson { get; set; } =
            string.Empty;

        public string MobileNo { get; set; } =
            string.Empty;

        public string Email { get; set; } =
            string.Empty;

        public string Address { get; set; } =
            string.Empty;

        public int PurchaseOrderCount { get; set; }
    }

    public sealed class PurchaserSaveRequestDto
    {
        [Required]
        [StringLength(200)]
        public string PurchaserName { get; set; } =
            string.Empty;

        [StringLength(200)]
        public string? ContactPerson { get; set; }

        [StringLength(50)]
        public string? MobileNo { get; set; }

        [EmailAddress]
        [StringLength(250)]
        public string? Email { get; set; }

        [StringLength(1000)]
        public string? Address { get; set; }
    }

    public sealed class PurchaserSaveResponseDto
    {
        public bool Success { get; set; }

        public string Message { get; set; } =
            string.Empty;

        public int PurchaserId { get; set; }
    }

    public sealed class PurchaserDeleteResponseDto
    {
        public bool Success { get; set; }

        public string Message { get; set; } =
            string.Empty;

        public int PurchaserId { get; set; }
    }
}

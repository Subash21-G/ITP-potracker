using System.ComponentModel.DataAnnotations;

namespace POTracker.Models
{
    public static class MaterialHistoryOutcomes
    {
        public const string PerfectDelivery = "PerfectDelivery";

        public const string ReworkAndDelivery = "ReworkAndDelivery";

        public const string Rejected = "Rejected";

        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var candidate = value.Trim();

            if (candidate.Equals(PerfectDelivery, StringComparison.OrdinalIgnoreCase))
            {
                return PerfectDelivery;
            }

            if (candidate.Equals(ReworkAndDelivery, StringComparison.OrdinalIgnoreCase))
            {
                return ReworkAndDelivery;
            }

            if (candidate.Equals(Rejected, StringComparison.OrdinalIgnoreCase))
            {
                return Rejected;
            }

            return null;
        }
    }

    public sealed class MaterialHistory
    {
        public int Id { get; set; }

        public int MaterialId { get; set; }

        public Material? Material { get; set; }

        [StringLength(40)]
        public string Outcome { get; set; } =
            MaterialHistoryOutcomes.PerfectDelivery;

        [StringLength(2000)]
        public string? Remark { get; set; }

        public DateTime OccurredDate { get; set; } = DateTime.Today;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; }
    }
}

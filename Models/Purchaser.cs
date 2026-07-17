using System.ComponentModel.DataAnnotations;

namespace POTracker.Models
{
    public class Purchaser
    {
        public int Id { get; set; }

        [Required]
        public string PurchaserName { get; set; } = string.Empty;

        public string? ContactPerson { get; set; }

        public string? MobileNo { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }

        public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    }
}
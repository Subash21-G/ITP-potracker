namespace POTracker.Models
{
    public class Alert
    {
        public int Id { get; set; }

        public int MaterialId { get; set; }

        public Material? Material { get; set; }

        public string AlertType { get; set; } = "Process Start";

        public string Message { get; set; } = string.Empty;

        public DateTime AlertDate { get; set; }

        public bool IsRead { get; set; } = false;

        public bool IsSentToMobile { get; set; } = false;
    }
}
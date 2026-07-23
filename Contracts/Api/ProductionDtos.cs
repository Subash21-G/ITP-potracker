namespace POTracker.Contracts.Api
{
    public sealed class ProcessQueueResponseDto
    {
        public ProcessQueueSummaryDto Summary { get; set; } = new();

        public List<ProcessQueueItemDto> Overdue { get; set; } = new();

        public List<ProcessQueueItemDto> StartToday { get; set; } = new();

        public List<ProcessQueueItemDto> ThisWeek { get; set; } = new();

        public List<ProcessQueueItemDto> Future { get; set; } = new();

        public List<ProcessQueueItemDto> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }
    }

    public sealed class ProcessQueueSummaryDto
    {
        public int TotalMaterials { get; set; }

        public int Overdue { get; set; }

        public int StartToday { get; set; }

        public int ThisWeek { get; set; }

        public int Future { get; set; }
    }

    public sealed class ProcessQueueItemDto
    {
        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string PurchaserName { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime? ProcessStartDate { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public DateTime? DispatchDate { get; set; }

        public string AssignedDashboard { get; set; } = string.Empty;

        public string ProcessStatus { get; set; } = string.Empty;

        public string DispatchStatus { get; set; } = string.Empty;
    }

    public sealed class ProductionCalendarEventDto
    {
        public int Id { get; set; }

        public int MaterialId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Start { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;

        public string EventType { get; set; } = string.Empty;

        public bool AllDay { get; set; } = true;
    }
}

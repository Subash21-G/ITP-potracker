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
        public string Id { get; set; } = string.Empty;

        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Start { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;

        public string EventType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public decimal OrderedQty { get; set; }

        public decimal DispatchQty { get; set; }

        public decimal PendingQty { get; set; }

        public DateTime? ProcessStartDate { get; set; }

        public DateTime? DispatchDate { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public string Handler { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;

        public int DaysOverdue { get; set; }

        public bool AllDay { get; set; } = true;
    }

    public sealed class ProductionCalendarDashboardDto
    {
        public int Overdue { get; set; }

        public int DueToday { get; set; }

        public int NextSevenDays { get; set; }

        public int CompletedThisMonth { get; set; }

        public int TotalMaterials { get; set; }

        public int ProcessStartEvents { get; set; }

        public int DispatchEvents { get; set; }

        public int DeliveryEvents { get; set; }

        public int OverdueEvents { get; set; }

        public int RevisedEvents { get; set; }

        public int CompletedEvents { get; set; }

        public List<ProductionCalendarUpcomingItemDto> Upcoming { get; set; }
            = new();

        public List<ProductionCalendarOverdueItemDto> OverdueItems { get; set; }
            = new();

        public List<ProductionCalendarHandlerWorkloadDto> HandlerWorkload { get; set; }
            = new();
    }

    public sealed class ProductionCalendarUpcomingItemDto
    {
        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string EventType { get; set; } = string.Empty;

        public DateTime EventDate { get; set; }

        public decimal PendingQty { get; set; }

        public string Unit { get; set; } = string.Empty;
    }

    public sealed class ProductionCalendarOverdueItemDto
    {
        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public DateTime DeliveryDate { get; set; }

        public int DaysOverdue { get; set; }
    }

    public sealed class ProductionCalendarHandlerWorkloadDto
    {
        public string Handler { get; set; } = string.Empty;

        public int ActiveMaterials { get; set; }

        public string Level { get; set; } = string.Empty;
    }
}
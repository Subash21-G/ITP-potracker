using System;
using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    public sealed class DrawingSummaryDto
    {
        public int TotalMaterials { get; init; }

        public int MissingDrawings { get; init; }

        public int UpdatedThisWeek { get; init; }

        public int WithVersion { get; init; }
    }

    public sealed class DrawingListItemDto
    {
        public int MaterialId { get; init; }

        public string MaterialNo { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string PONumber { get; init; } = string.Empty;

        public string DrawingNo { get; set; } = string.Empty;

        public string DrawingVersion { get; init; } = string.Empty;

        public DateTime? DrawingUpdatedDate { get; init; }

        public string? DrawingFilePath { get; set; }

        public string? HandSketchFilePath { get; set; }

        public string? OutsourceDrawingFilePath { get; set; }

        public string? WebDrawingFilePath { get; set; }

        public string? PdfFilePath { get; set; }

        public bool HasDrawing { get; init; }
    }

    public sealed class DrawingListResponseDto
    {
        public DrawingSummaryDto Summary { get; init; } = new();

        public List<DrawingListItemDto> Items { get; init; } = new();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalRecords { get; init; }

        public int TotalPages { get; init; }
    }

    public sealed class AuditItemDto
    {
        public int MaterialId { get; init; }

        public DateTime CreatedDate { get; init; }

        public string MaterialNo { get; init; } = string.Empty;

        public string PONumber { get; init; } = string.Empty;

        public string Action { get; init; } = string.Empty;

        public string ProcessStatus { get; init; } = string.Empty;

        public string DispatchStatus { get; init; } = string.Empty;

        public string Owner { get; init; } = string.Empty;
    }

    public sealed class AuditResponseDto
    {
        public List<AuditItemDto> Items { get; init; } = new();

        public int RecordCount { get; init; }

        public DateTime GeneratedAt { get; init; }
    }

    public sealed class AnalyticsSummaryDto
    {
        public int PurchaserCount { get; init; }

        public int TotalMaterials { get; init; }

        public int OverdueMaterials { get; init; }

        public int PurchaseOrderCount { get; init; }
    }

    public sealed class PurchaserAnalyticsRowDto
    {
        public int PurchaserId { get; init; }

        public string PurchaserName { get; init; } = string.Empty;

        public int PurchaseOrderCount { get; init; }

        public int MaterialCount { get; init; }

        public int PendingMaterials { get; init; }

        public int CompletedMaterials { get; init; }
    }

    public sealed class AnalyticsResponseDto
    {
        public AnalyticsSummaryDto Summary { get; init; } = new();

        public List<PurchaserAnalyticsRowDto> Purchasers { get; init; } =
            new();

        public DateTime GeneratedAt { get; init; }
    }
}

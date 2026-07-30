using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    /// <summary>
    /// Purchase Order identifiers selected for bulk deletion.
    /// </summary>
    public sealed class PurchaseOrderBulkDeleteRequestDto
    {
        public List<int> PurchaseOrderIds { get; init; } = new();
    }

    /// <summary>
    /// Result returned after attempting to delete multiple Purchase Orders.
    /// Orders with linked materials are safely skipped.
    /// </summary>
    public sealed class PurchaseOrderBulkDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int RequestedCount { get; init; }

        public int DeletedCount { get; init; }

        public int SkippedCount { get; init; }

        public int MissingCount { get; init; }

        public int LinkedMaterialCount { get; init; }

        public List<int> DeletedPurchaseOrderIds { get; init; } = new();
    }
}
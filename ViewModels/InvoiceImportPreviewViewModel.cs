using System;
using System.Collections.Generic;

namespace POTracker.ViewModels
{
    public class InvoiceImportPreviewViewModel
    {
        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime? InvoiceDate { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string UploadedFileName { get; set; } = string.Empty;

        public string UploadedFilePath { get; set; } = string.Empty;

        public List<InvoiceImportItemViewModel> Items { get; set; } = new();
    }

    public class InvoiceImportItemViewModel
    {
        public DateTime? PODate { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string HsnSacCode { get; set; } = string.Empty;

        public decimal InvoiceQty { get; set; }

        public decimal PriceUnit { get; set; }

        public decimal GstAmount { get; set; }

        public decimal Amount { get; set; }
    }
}
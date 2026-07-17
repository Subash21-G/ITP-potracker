using Microsoft.AspNetCore.Http;

namespace POTracker.ViewModels
{
    public class POImportPreviewViewModel
    {
        // =====================================================
        // PO HEADER DETAILS
        // =====================================================

        public string? PurchaseOrderNo { get; set; }

        // Date of Order from PO PDF
        public DateTime? PODate { get; set; }

        // Date of Emission from PO PDF
        // Treated as PO received date
        public DateTime? ReceivedDate { get; set; }

        // Buyer / Contact person
        public string? BuyerContactPerson { get; set; }

        // Buyer email address
        public string? BuyerEmail { get; set; }

        // =====================================================
        // VENDOR DETAILS
        // =====================================================

        public string? VendorCode { get; set; }

        public string? VendorName { get; set; }

        // =====================================================
        // COMMERCIAL DETAILS
        // =====================================================

        public string? PaymentTerms { get; set; }

        public string? DeliveryCondition { get; set; }

        public string? Currency { get; set; }

        public decimal BasicValue { get; set; }

        public decimal TaxValue { get; set; }

        public decimal GrandTotal { get; set; }

        // Example: WEG
        public string? ImportFormat { get; set; }

        // =====================================================
        // UPLOADED PDF
        // =====================================================

        public string? UploadedPdfPath { get; set; }

        // =====================================================
        // IMPORT WARNINGS
        // =====================================================

        public List<string> Warnings { get; set; } = new();

        // =====================================================
        // PO MATERIAL ITEMS
        // =====================================================

        public List<POImportItemViewModel> Items { get; set; } = new();
    }

    public class POImportItemViewModel
    {
        // =====================================================
        // ITEM BASIC DETAILS
        // =====================================================

        // Example: N-10, M-20, C-30
        public string? LineItem { get; set; }

        // N = New
        // M = Modified
        // C = Completely Supplied
        public string? ItemType { get; set; }

        // May be blank in some WEG POs
        public string? MaterialNo { get; set; }

        public string? HsnSacCode { get; set; }

        public string? ShortDescription { get; set; }

        public string? FullDescription { get; set; }

        public string? DrawingNo { get; set; }

        // =====================================================
        // RECEIVER DETAILS
        // =====================================================

        public string? Receiver { get; set; }

        public string? UnloadingPoint { get; set; }

        // =====================================================
        // QUANTITY DETAILS
        // =====================================================

        public decimal Quantity { get; set; }

        public string? Unit { get; set; }

        public string? Dep { get; set; }

        // =====================================================
        // COST DETAILS
        // =====================================================

        public decimal ValueOfGoods { get; set; }

        public string? PriceUnit { get; set; }

        public decimal TotalValue { get; set; }

        // =====================================================
        // TAX DETAILS
        // =====================================================

        public decimal CGSTRate { get; set; }

        public decimal CGSTAmount { get; set; }

        public decimal SGSTRate { get; set; }

        public decimal SGSTAmount { get; set; }

        public decimal IGSTRate { get; set; }

        public decimal IGSTAmount { get; set; }

        public decimal TotalValueWithTax { get; set; }

        // =====================================================
        // DELIVERY DETAILS
        // =====================================================

        public DateTime? DeliveryDate { get; set; }

        // =====================================================
        // OPTIONAL DRAWING UPLOAD
        // =====================================================

        public IFormFile? DrawingFile { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POTracker.Models
{
    public class Material
    {
        public int Id { get; set; }

        // =====================================================
        // PURCHASE ORDER RELATIONSHIP
        // =====================================================

        public int PurchaseOrderId { get; set; }

        public PurchaseOrder? PurchaseOrder { get; set; }

        // =====================================================
        // PO ITEM BASIC DETAILS
        // =====================================================

        // Example: 10, 20, 30, 40
        [StringLength(50)]
        public string? LineItem { get; set; }

        // WEG item type:
        // N = New
        // M = Modified
        // C = Completely Supplied
        [StringLength(10)]
        public string? ItemType { get; set; }

        /*
         * Material number is not available in some WEG PO formats.
         * Therefore this field must remain nullable and must not
         * contain the [Required] attribute.
         */
        [StringLength(150)]
        public string? MaterialNo { get; set; }

        [StringLength(100)]
        public string? HsnSacCode { get; set; }

        [StringLength(500)]
        public string? ShortDescription { get; set; }

        public string? BriefDescription { get; set; }

        // Example: Suresh R
        [StringLength(200)]
        public string? Receiver { get; set; }

        // Example: WINDING
        [StringLength(200)]
        public string? UnloadingPoint { get; set; }

        // =====================================================
        // QUANTITY DETAILS
        // =====================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal Qty { get; set; }

        [StringLength(50)]
        public string? Unit { get; set; }

        [StringLength(100)]
        public string? Dep { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DispatchQty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PendingQty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal StockQty { get; set; }

        // =====================================================
        // INVOICE DETAILS
        // =====================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal InvoiceQty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceQty { get; set; }

        [StringLength(50)]
        public string InvoiceStatus { get; set; } = "Not Invoiced";

        // =====================================================
        // COST DETAILS
        // =====================================================

        /*
         * Item rate.
         * Example from WEG PO: 6145.00
         */
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        /*
         * Value shown under Value of Goods.
         */
        [Column(TypeName = "decimal(18,2)")]
        public decimal ValueOfGoods { get; set; }

        /*
         * Example: UN
         */
        [StringLength(50)]
        public string? PriceUnit { get; set; }

        /*
         * Quantity × Rate before tax.
         */
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalValue { get; set; }

        // =====================================================
        // TAX DETAILS
        // =====================================================

        [Column(TypeName = "decimal(8,2)")]
        public decimal CGSTRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CGSTAmount { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal SGSTRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SGSTAmount { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal IGSTRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal IGSTAmount { get; set; }

        /*
         * TotalValue + CGSTAmount + SGSTAmount + IGSTAmount
         */
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalValueWithTax { get; set; }

        // =====================================================
        // RAW MATERIAL DETAILS
        // =====================================================

        [StringLength(200)]
        public string? RMGrade { get; set; }

        [StringLength(200)]
        public string? RMSize { get; set; }

        [StringLength(200)]
        public string? RMSource { get; set; }

        [StringLength(200)]
        public string? RMProcess { get; set; }

        // =====================================================
        // SURFACE / MACHINING
        // =====================================================

        [StringLength(200)]
        public string? SurfaceFinish { get; set; }

        [StringLength(200)]
        public string? MachiningType { get; set; }

        // =====================================================
        // PROCESS TIMES
        // =====================================================

        [StringLength(100)]
        public string? ProcessTime { get; set; }

        [StringLength(100)]
        public string? MaterialProcessTime { get; set; }

        // =====================================================
        // DATE DETAILS
        // =====================================================

        public DateTime? PODate { get; set; }

        public DateTime? ReceivingDate { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public DateTime? AlertDate { get; set; }

        public DateTime? DispatchDate { get; set; }

        public DateTime? DrawingUpdatedDate { get; set; }

        // =====================================================
        // DRAWING DETAILS
        // =====================================================

        [StringLength(200)]
        public string? DrawingNo { get; set; }

        [StringLength(100)]
        public string? DrawingVersion { get; set; }

        [StringLength(500)]
        public string? DrawingFilePath { get; set; }

        [StringLength(500)]
        public string? HandSketchFilePath { get; set; }

        [StringLength(500)]
        public string? OutsourceDrawingFilePath { get; set; }

        [StringLength(500)]
        public string? WebDrawingFilePath { get; set; }

        [StringLength(500)]
        public string? PDFFilePath { get; set; }

        // =====================================================
        // PROCESS STATUS
        // =====================================================

        public DateTime? ProcessStartDate { get; set; }

        [StringLength(50)]
        public string ProcessStatus { get; set; } = "Pending";

        [StringLength(50)]
        public string DispatchStatus { get; set; } = "Pending";

        // =====================================================
        // DASHBOARD ASSIGNMENT
        // =====================================================

        [StringLength(100)]
        public string? AssignedDashboard { get; set; }

        // =====================================================
        // PLANNING CLASSIFICATION
        // =====================================================

        [StringLength(20)]
        public string SourcingType { get; set; } = "Inhouse";

        [StringLength(200)]
        public string? OutsourcedTo { get; set; }

        public bool HasCostVariation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? VariationCost { get; set; }

        [StringLength(20)]
        public string PriorityLevel { get; set; } = "Normal";

        // =====================================================
        // REMARKS
        // =====================================================

        public string? Remark { get; set; }

        // =====================================================
        // CREATED / UPDATED DATE
        // =====================================================

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; }

        // =====================================================
        // NAVIGATION PROPERTIES
        // =====================================================

        public ICollection<InvoiceItem> InvoiceItems { get; set; }
            = new List<InvoiceItem>();

        public ICollection<Dispatch> Dispatches { get; set; }
            = new List<Dispatch>();

        public ICollection<MaterialHistory> HistoryEntries { get; set; }
            = new List<MaterialHistory>();
    }
}

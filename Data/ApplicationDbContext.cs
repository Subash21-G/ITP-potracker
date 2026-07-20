using Microsoft.EntityFrameworkCore;
using POTracker.Models;

namespace POTracker.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppUser> AppUsers { get; set; }

        public DbSet<Purchaser> Purchasers { get; set; }

        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }

        public DbSet<Material> Materials { get; set; }

        public DbSet<Stock> Stocks { get; set; }

        public DbSet<Alert> Alerts { get; set; }

        public DbSet<VyaparInvoiceImportBatch> VyaparInvoiceImportBatches
        {
            get;
            set;
        }

        public DbSet<VyaparInvoiceImportRow> VyaparInvoiceImportRows
        {
            get;
            set;
        }

        public DbSet<Invoice> Invoices { get; set; }

        public DbSet<InvoiceItem> InvoiceItems { get; set; }

        public DbSet<Dispatch> Dispatches { get; set; }

        public DbSet<PendingInvoiceItem> PendingInvoiceItems { get; set; }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =====================================================
            // USER LOOKUP
            // =====================================================

            modelBuilder.Entity<AppUser>()
                .HasIndex(x => x.UserName)
                .IsUnique();

            // =====================================================
            // PURCHASE ORDER
            // =====================================================

            modelBuilder.Entity<PurchaseOrder>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.PONumber)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<PurchaseOrder>()
                .HasIndex(x => x.PONumber)
                .IsUnique();

            modelBuilder.Entity<PurchaseOrder>()
                .HasIndex(x => new
                {
                    x.Status,
                    x.CreatedDate
                });

            modelBuilder.Entity<PurchaseOrder>()
                .HasIndex(x => new
                {
                    x.PurchaserId,
                    x.PODate
                });

            modelBuilder.Entity<PurchaseOrder>()
                .HasIndex(x => x.ReceivedDate);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.BuyerContactPerson)
                .HasMaxLength(200);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.BuyerEmail)
                .HasMaxLength(250);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.VendorCode)
                .HasMaxLength(100);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.VendorName)
                .HasMaxLength(250);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.PaymentTerms)
                .HasMaxLength(250);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.DeliveryCondition)
                .HasMaxLength(250);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.Currency)
                .HasMaxLength(50);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.ImportFormat)
                .HasMaxLength(100);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.PdfFilePath)
                .HasMaxLength(500);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.Status)
                .HasMaxLength(50);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.BasicValue)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.TaxValue)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PurchaseOrder>()
                .Property(x => x.GrandTotal)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(x => x.Purchaser)
                .WithMany()
                .HasForeignKey(x => x.PurchaserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseOrder>()
                .HasMany(x => x.Materials)
                .WithOne(x => x.PurchaseOrder)
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // =====================================================
            // MATERIAL
            // =====================================================

            modelBuilder.Entity<Material>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<Material>()
                .HasIndex(x => new
                {
                    x.PurchaseOrderId,
                    x.LineItem
                });

            modelBuilder.Entity<Material>()
                .HasIndex(x => x.MaterialNo);

            modelBuilder.Entity<Material>()
                .HasIndex(x => new
                {
                    x.PurchaseOrderId,
                    x.MaterialNo,
                    x.LineItem
                });

            modelBuilder.Entity<Material>()
                .HasIndex(x => new
                {
                    x.DispatchStatus,
                    x.DeliveryDate
                });

            modelBuilder.Entity<Material>()
                .HasIndex(x => new
                {
                    x.InvoiceStatus,
                    x.CreatedDate
                });

            modelBuilder.Entity<Material>()
                .HasIndex(x => new
                {
                    x.ProcessStatus,
                    x.ProcessStartDate
                });

            modelBuilder.Entity<Material>()
                .Property(x => x.LineItem)
                .HasMaxLength(50);

            modelBuilder.Entity<Material>()
                .Property(x => x.ItemType)
                .HasMaxLength(10);

            /*
             * Material Number is optional.
             * Some WEG purchase orders do not contain a material number.
             */
            modelBuilder.Entity<Material>()
                .Property(x => x.MaterialNo)
                .HasMaxLength(150)
                .IsRequired(false);

            modelBuilder.Entity<Material>()
                .Property(x => x.HsnSacCode)
                .HasMaxLength(100)
                .IsRequired(false);

            modelBuilder.Entity<Material>()
                .Property(x => x.ShortDescription)
                .HasMaxLength(500);

            modelBuilder.Entity<Material>()
                .Property(x => x.Receiver)
                .HasMaxLength(200);

            modelBuilder.Entity<Material>()
                .Property(x => x.UnloadingPoint)
                .HasMaxLength(200);

            modelBuilder.Entity<Material>()
                .Property(x => x.Unit)
                .HasMaxLength(50);

            modelBuilder.Entity<Material>()
                .Property(x => x.Dep)
                .HasMaxLength(100);

            modelBuilder.Entity<Material>()
                .Property(x => x.PriceUnit)
                .HasMaxLength(50);

            modelBuilder.Entity<Material>()
                .Property(x => x.InvoiceStatus)
                .HasMaxLength(50);

            modelBuilder.Entity<Material>()
                .Property(x => x.ProcessStatus)
                .HasMaxLength(50);

            modelBuilder.Entity<Material>()
                .Property(x => x.DispatchStatus)
                .HasMaxLength(50);

            modelBuilder.Entity<Material>()
                .Property(x => x.AssignedDashboard)
                .HasMaxLength(100);

            modelBuilder.Entity<Material>()
                .Property(x => x.DrawingNo)
                .HasMaxLength(200);

            modelBuilder.Entity<Material>()
                .Property(x => x.DrawingVersion)
                .HasMaxLength(100);

            modelBuilder.Entity<Material>()
                .Property(x => x.DrawingFilePath)
                .HasMaxLength(500);

            modelBuilder.Entity<Material>()
                .Property(x => x.PDFFilePath)
                .HasMaxLength(500);

            // =====================================================
            // MATERIAL QUANTITY FIELDS
            // =====================================================

            modelBuilder.Entity<Material>()
                .Property(x => x.Qty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.DispatchQty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.PendingQty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.StockQty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.InvoiceQty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.BalanceQty)
                .HasColumnType("decimal(18,2)");

            // =====================================================
            // MATERIAL COST FIELDS
            // =====================================================

            modelBuilder.Entity<Material>()
                .Property(x => x.Cost)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.ValueOfGoods)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.TotalValue)
                .HasColumnType("decimal(18,2)");

            // =====================================================
            // MATERIAL TAX FIELDS
            // =====================================================

            modelBuilder.Entity<Material>()
                .Property(x => x.CGSTRate)
                .HasColumnType("decimal(8,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.CGSTAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.SGSTRate)
                .HasColumnType("decimal(8,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.SGSTAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.IGSTRate)
                .HasColumnType("decimal(8,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.IGSTAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Material>()
                .Property(x => x.TotalValueWithTax)
                .HasColumnType("decimal(18,2)");

            // =====================================================
            // VYAPAR INVOICE IMPORT BATCH
            // =====================================================

            modelBuilder.Entity<VyaparInvoiceImportBatch>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<VyaparInvoiceImportBatch>()
                .HasIndex(x => x.ImportDate);

            modelBuilder.Entity<VyaparInvoiceImportBatch>()
                .Property(x => x.FileName)
                .HasMaxLength(250);

            // =====================================================
            // VYAPAR INVOICE IMPORT ROW
            // =====================================================

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .HasOne(x => x.Batch)
                .WithMany(x => x.Rows)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .Property(x => x.PONumber)
                .HasMaxLength(100);

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .Property(x => x.LineItem)
                .HasMaxLength(50);

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .Property(x => x.MaterialNumber)
                .HasMaxLength(100);

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .Property(x => x.InvoiceNumber)
                .HasMaxLength(100);

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .Property(x => x.Status)
                .HasMaxLength(50);

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .Property(x => x.InvoiceQty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .HasIndex(x => new
                {
                    x.InvoiceNumber,
                    x.Status
                });

            modelBuilder.Entity<VyaparInvoiceImportRow>()
                .HasIndex(x => new
                {
                    x.PONumber,
                    x.MaterialNumber,
                    x.LineItem
                });

            // =====================================================
            // INVOICE MASTER
            // =====================================================

            modelBuilder.Entity<Invoice>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<Invoice>()
                .Property(x => x.InvoiceNumber)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Invoice>()
                .HasIndex(x => x.InvoiceNumber)
                .IsUnique(false);

            modelBuilder.Entity<Invoice>()
                .HasIndex(x => x.CreatedDate);

            modelBuilder.Entity<Invoice>()
                .HasIndex(x => x.InvoiceDate);

            // =====================================================
            // INVOICE ITEMS
            // =====================================================

            modelBuilder.Entity<InvoiceItem>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<InvoiceItem>()
                .Property(x => x.PONumber)
                .HasMaxLength(100);

            modelBuilder.Entity<InvoiceItem>()
                .Property(x => x.LineItem)
                .HasMaxLength(50);

            modelBuilder.Entity<InvoiceItem>()
                .Property(x => x.MaterialNo)
                .HasMaxLength(150);

            modelBuilder.Entity<InvoiceItem>()
                .HasOne(x => x.Invoice)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoiceItem>()
                .HasOne(x => x.Material)
                .WithMany(x => x.InvoiceItems)
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvoiceItem>()
                .Property(x => x.InvoiceQty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<InvoiceItem>()
                .Property(x => x.PriceUnit)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<InvoiceItem>()
                .Property(x => x.GstAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<InvoiceItem>()
                .Property(x => x.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<InvoiceItem>()
                .HasIndex(x => new
                {
                    x.InvoiceId,
                    x.MaterialId,
                    x.LineItem
                });

            modelBuilder.Entity<InvoiceItem>()
                .HasIndex(x => new
                {
                    x.PONumber,
                    x.MaterialNo,
                    x.LineItem
                });

            // =====================================================
            // DISPATCH
            // =====================================================

            modelBuilder.Entity<Dispatch>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<Dispatch>()
                .Property(x => x.DispatchNo)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Dispatch>()
                .HasOne(x => x.Material)
                .WithMany(x => x.Dispatches)
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Dispatch>()
                .Property(x => x.DispatchQty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Dispatch>()
                .HasIndex(x => new
                {
                    x.MaterialId,
                    x.DispatchDate
                });

            modelBuilder.Entity<Dispatch>()
                .HasIndex(x => x.DispatchNo);

            // =====================================================
            // PENDING INVOICE ITEMS
            // =====================================================

            modelBuilder.Entity<PendingInvoiceItem>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<PendingInvoiceItem>()
                .HasOne(x => x.Invoice)
                .WithMany()
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.InvoiceNumber)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.PONumber)
                .HasMaxLength(100);

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.LineItem)
                .HasMaxLength(50);

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.MaterialNo)
                .HasMaxLength(100);

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.Status)
                .HasMaxLength(50);

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.InvoiceQty)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.PriceUnit)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.GstAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PendingInvoiceItem>()
                .Property(x => x.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PendingInvoiceItem>()
                .HasIndex(x => new
                {
                    x.Status,
                    x.CreatedDate
                });

            modelBuilder.Entity<PendingInvoiceItem>()
                .HasIndex(x => new
                {
                    x.PONumber,
                    x.MaterialNo,
                    x.LineItem
                });

            modelBuilder.Entity<PendingInvoiceItem>()
                .HasIndex(x => new
                {
                    x.InvoiceId,
                    x.Status
                });
        }
    }
}
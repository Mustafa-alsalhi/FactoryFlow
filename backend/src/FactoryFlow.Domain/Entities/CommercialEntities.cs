using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Enums;

namespace FactoryFlow.Domain.Entities;

public class Supplier : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PurchaseOrder : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDate { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public Supplier? Supplier { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid RawMaterialId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public RawMaterial? RawMaterial { get; set; }
}

public class PurchaseInvoice : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public bool IsReturn { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? IdempotencyKey { get; set; }

    public Supplier? Supplier { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
}

public class PurchaseInvoiceItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PurchaseInvoiceId { get; set; }
    public Guid RawMaterialId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }

    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public RawMaterial? RawMaterial { get; set; }
}

public class Customer : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SalesOrder : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public Customer? Customer { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}

public class SalesOrderItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }

    public SalesOrder? SalesOrder { get; set; }
    public Product? Product { get; set; }
}

public class SalesInvoice : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? SalesOrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public bool IsReturn { get; set; }
    public bool IsCashSale { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TotalCogs { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? IdempotencyKey { get; set; }

    public Customer? Customer { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}

public class SalesInvoiceItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SalesInvoiceId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }

    public SalesInvoice? SalesInvoice { get; set; }
    public Product? Product { get; set; }
}


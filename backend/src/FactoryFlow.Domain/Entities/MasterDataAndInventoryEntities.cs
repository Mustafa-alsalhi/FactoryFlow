using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Enums;

namespace FactoryFlow.Domain.Entities;

public class UnitOfMeasure : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
}

public class UnitConversion : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FromUnitId { get; set; }
    public Guid ToUnitId { get; set; }
    public decimal Factor { get; set; }

    public UnitOfMeasure? FromUnit { get; set; }
    public UnitOfMeasure? ToUnit { get; set; }
}

public class Category : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string CategoryFor { get; set; } = "Both"; // RawMaterial, Product, Both
}

public class RawMaterial : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public decimal StandardCost { get; set; }
    public decimal LastPurchasePrice { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public bool TrackBatch { get; set; } = true;
    public bool TrackExpiry { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public Category? Category { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
    public Supplier? PreferredSupplier { get; set; }
}

public class Product : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? Specifications { get; set; }
    public string? ImageUrl { get; set; }
    public ProductType ProductType { get; set; } = ProductType.Finished;
    public Guid CategoryId { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal StandardCost { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? Category { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
}

public class Warehouse : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;

    public Branch? Branch { get; set; }
}

public class StockBalance : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? RawMaterialId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AverageUnitCost { get; set; }
    public decimal TotalValue { get; set; }

    public Warehouse? Warehouse { get; set; }
    public RawMaterial? RawMaterial { get; set; }
    public Product? Product { get; set; }
}

public class InventoryBatch : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? RawMaterialId { get; set; }
    public Guid? ProductId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal InitialQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class InventoryTransaction : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public StockTransactionType TransactionType { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public Guid? RawMaterialId { get; set; }
    public Guid? ProductId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }

    public Warehouse? Warehouse { get; set; }
    public RawMaterial? RawMaterial { get; set; }
    public Product? Product { get; set; }
}


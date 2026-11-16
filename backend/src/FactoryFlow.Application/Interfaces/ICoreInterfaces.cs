using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactoryFlow.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }
    string? IpAddress { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
}

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Branch> Branches { get; }
    DbSet<AppUser> Users { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<LoginLog> LoginLogs { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Notification> Notifications { get; }

    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<UnitConversion> UnitConversions { get; }
    DbSet<Category> Categories { get; }
    DbSet<RawMaterial> RawMaterials { get; }
    DbSet<Product> Products { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<StockBalance> StockBalances { get; }
    DbSet<InventoryBatch> InventoryBatches { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }

    DbSet<BillOfMaterials> BillsOfMaterials { get; }
    DbSet<BomItem> BomItems { get; }
    DbSet<ProductionLine> ProductionLines { get; }
    DbSet<Machine> Machines { get; }
    DbSet<MachineMaintenance> MachineMaintenances { get; }
    DbSet<Employee> Employees { get; }
    DbSet<ProductionOrder> ProductionOrders { get; }
    DbSet<ProductionMaterialIssue> ProductionMaterialIssues { get; }
    DbSet<ProductionLaborLog> ProductionLaborLogs { get; }
    DbSet<ProductionOverheadLog> ProductionOverheadLogs { get; }
    DbSet<ProductionOutput> ProductionOutputs { get; }
    DbSet<QualityInspection> QualityInspections { get; }

    DbSet<Supplier> Suppliers { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
    DbSet<PurchaseInvoice> PurchaseInvoices { get; }
    DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; }

    DbSet<Customer> Customers { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderItem> SalesOrderItems { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<SalesInvoiceItem> SalesInvoiceItems { get; }

    DbSet<Currency> Currencies { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<TaxRate> TaxRates { get; }
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }
    DbSet<Cashbox> Cashboxes { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<TreasuryVoucher> TreasuryVouchers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public record JournalLineInput(string AccountKeyOrId, decimal Debit, decimal Credit, string? Memo = null, string? CurrencyCode = null, decimal ExchangeRate = 1m, decimal ForeignAmount = 0m);

public interface IAccountingService
{
    Task<JournalEntry> CreateAndPostAsync(
        Guid tenantId,
        DateTime entryDate,
        string description,
        string? referenceType,
        Guid? referenceId,
        List<JournalLineInput> lines,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task<JournalEntry> ReverseEntryAsync(Guid tenantId, Guid journalEntryId, string reason, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task<InventoryTransaction> ReceiveAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid? rawMaterialId,
        Guid? productId,
        decimal quantity,
        decimal unitCost,
        StockTransactionType type,
        string? batchNumber,
        DateTime? expiryDate,
        string? referenceType,
        Guid? referenceId,
        string? notes = null,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task<InventoryTransaction> IssueAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid? rawMaterialId,
        Guid? productId,
        decimal quantity,
        StockTransactionType type,
        string? referenceType,
        Guid? referenceId,
        string? notes = null,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task<(InventoryTransaction OutTx, InventoryTransaction InTx)> TransferAsync(
        Guid tenantId,
        Guid fromWarehouseId,
        Guid toWarehouseId,
        Guid? rawMaterialId,
        Guid? productId,
        decimal quantity,
        string? notes = null,
        CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(Guid tenantId, string action, string entityName, string entityId, object? oldValues = null, object? newValues = null, CancellationToken ct = default);
}

public interface INotificationService
{
    Task CreateAsync(Guid tenantId, NotificationType type, string title, string titleAr, string message, string messageAr, string? referenceEntity = null, Guid? referenceId = null, CancellationToken ct = default);
}

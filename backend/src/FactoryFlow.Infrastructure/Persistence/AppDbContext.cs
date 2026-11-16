using System.Linq.Expressions;
using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactoryFlow.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public Guid? CurrentTenantId => _currentUser.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<UnitConversion> UnitConversions => Set<UnitConversion>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RawMaterial> RawMaterials => Set<RawMaterial>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<BillOfMaterials> BillsOfMaterials => Set<BillOfMaterials>();
    public DbSet<BomItem> BomItems => Set<BomItem>();
    public DbSet<ProductionLine> ProductionLines => Set<ProductionLine>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachineMaintenance> MachineMaintenances => Set<MachineMaintenance>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionMaterialIssue> ProductionMaterialIssues => Set<ProductionMaterialIssue>();
    public DbSet<ProductionLaborLog> ProductionLaborLogs => Set<ProductionLaborLog>();
    public DbSet<ProductionOverheadLog> ProductionOverheadLogs => Set<ProductionOverheadLog>();
    public DbSet<ProductionOutput> ProductionOutputs => Set<ProductionOutput>();
    public DbSet<QualityInspection> QualityInspections => Set<QualityInspection>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();

    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<Cashbox> Cashboxes => Set<Cashbox>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<TreasuryVoucher> TreasuryVouchers => Set<TreasuryVoucher>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        foreach (var relationship in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType) && typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var isDeletedProp = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var notDeleted = Expression.Not(isDeletedProp);

                var tenantProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var currentTenantExpr = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                var tenantNull = Expression.Equal(currentTenantExpr, Expression.Constant(null, typeof(Guid?)));
                var tenantMatch = Expression.Equal(
                    Expression.Convert(tenantProp, typeof(Guid?)),
                    currentTenantExpr);
                var tenantFilter = Expression.OrElse(tenantNull, tenantMatch);

                var combined = Expression.AndAlso(notDeleted, tenantFilter);
                var lambda = Expression.Lambda(combined, parameter);
                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
            else if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var isDeletedProp = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var lambda = Expression.Lambda(Expression.Not(isDeletedProp), parameter);
                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        builder.Entity<Tenant>().HasIndex(t => t.Code).IsUnique();
        builder.Entity<AppUser>().HasIndex(u => new { u.TenantId, u.NormalizedEmail }).IsUnique();
        builder.Entity<AppRole>().HasIndex(r => new { r.TenantId, r.NormalizedName }).IsUnique();
        builder.Entity<RawMaterial>().HasIndex(r => new { r.TenantId, r.Code }).IsUnique();
        builder.Entity<Product>().HasIndex(p => new { p.TenantId, p.Sku }).IsUnique();
        builder.Entity<Account>().HasIndex(a => new { a.TenantId, a.Code }).IsUnique();
        builder.Entity<JournalEntry>().HasIndex(j => new { j.TenantId, j.EntryNumber }).IsUnique();
        builder.Entity<ProductionOrder>().HasIndex(p => new { p.TenantId, p.OrderNumber }).IsUnique();
        builder.Entity<PurchaseOrder>().HasIndex(p => new { p.TenantId, p.OrderNumber }).IsUnique();
        builder.Entity<PurchaseInvoice>().HasIndex(p => new { p.TenantId, p.InvoiceNumber }).IsUnique();
        builder.Entity<SalesOrder>().HasIndex(s => new { s.TenantId, s.OrderNumber }).IsUnique();
        builder.Entity<SalesInvoice>().HasIndex(s => new { s.TenantId, s.InvoiceNumber }).IsUnique();
        builder.Entity<StockBalance>().HasIndex(s => new { s.TenantId, s.WarehouseId, s.RawMaterialId, s.ProductId });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var userEmail = _currentUser.Email ?? "system";
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy ??= userEmail;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userEmail;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName?.Contains("InMemory") == true)
        {
            return Task.FromResult<IDbContextTransaction>(new NoOpTransaction());
        }
        return Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class NoOpTransaction : IDbContextTransaction
    {
        public Guid TransactionId => Guid.NewGuid();
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

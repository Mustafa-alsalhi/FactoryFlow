using FactoryFlow.Application.Common;
using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using FactoryFlow.Infrastructure.Persistence;
using FactoryFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

Console.WriteLine("=== FactoryFlow ERP Automated Test Suite ===");
int passed = 0;
int failed = 0;

async Task RunTest(string name, Func<Task> test)
{
    try
    {
        await test();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[PASS] {name}");
        Console.ResetColor();
        passed++;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {name}: {ex.Message}");
        Console.ResetColor();
        failed++;
    }
}

void AssertTrue(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{message} (Expected: {expected}, Actual: {actual})");
}

var testConn = "Server=(localdb)\\MSSQLLocalDB;Database=FactoryFlowTestDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

AppDbContext CreateDb(Guid? tenantId = null)
{
    var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(testConn).Options;
    return new AppDbContext(opts, new TestCurrentUserService(tenantId));
}

// Recreate clean test DB
await using (var initDb = CreateDb(null))
{
    await initDb.Database.EnsureDeletedAsync();
    await initDb.Database.EnsureCreatedAsync();
}

await RunTest("1. Multi-Tenancy Isolation Filter Prevents Cross-Tenant Reads", async () =>
{
    var t1 = Guid.NewGuid();
    var t2 = Guid.NewGuid();
    await using (var db = CreateDb(null))
    {
        db.Tenants.AddRange(
            new Tenant { Id = t1, Code = "T1", Name = "Tenant 1" },
            new Tenant { Id = t2, Code = "T2", Name = "Tenant 2" });
        var cat1 = new Category { TenantId = t1, Code = "C1", Name = "Cat 1" };
        var cat2 = new Category { TenantId = t2, Code = "C2", Name = "Cat 2" };
        var uom1 = new UnitOfMeasure { TenantId = t1, Code = "KG", Name = "Kg" };
        var uom2 = new UnitOfMeasure { TenantId = t2, Code = "KG", Name = "Kg" };
        db.Categories.AddRange(cat1, cat2);
        db.UnitsOfMeasure.AddRange(uom1, uom2);
        db.RawMaterials.AddRange(
            new RawMaterial { TenantId = t1, Code = "RM-A", Name = "Material A", CategoryId = cat1.Id, UnitOfMeasureId = uom1.Id },
            new RawMaterial { TenantId = t2, Code = "RM-B", Name = "Material B", CategoryId = cat2.Id, UnitOfMeasureId = uom2.Id });
        await db.SaveChangesAsync();
    }

    await using (var dbT1 = CreateDb(t1))
    {
        var items = await dbT1.RawMaterials.ToListAsync();
        AssertEqual(1, items.Count, "Tenant 1 should only see 1 raw material");
        AssertEqual("RM-A", items[0].Code, "Tenant 1 should only see RM-A");
    }
});

await RunTest("2. Double-Entry Accounting Enforces Debit == Credit and Reversal", async () =>
{
    var tId = Guid.NewGuid();
    await using var db = CreateDb(tId);
    db.Tenants.Add(new Tenant { Id = tId, Code = "ACC1", Name = "Acc Tenant" });
    db.Accounts.AddRange(
        new Account { TenantId = tId, Code = "1010", Name = "Cash", AccountType = AccountType.Asset, SystemKey = SystemAccountKeys.Cash },
        new Account { TenantId = tId, Code = "4010", Name = "Sales", AccountType = AccountType.Revenue, SystemKey = SystemAccountKeys.SalesRevenue });
    await db.SaveChangesAsync();

    var accSvc = new AccountingService(db);

    bool threw = false;
    try
    {
        await accSvc.CreateAndPostAsync(tId, DateTime.UtcNow, "Unbalanced", null, null,
            new List<JournalLineInput>
            {
                new(SystemAccountKeys.Cash, 100m, 0m),
                new(SystemAccountKeys.SalesRevenue, 0m, 90m)
            });
    }
    catch (DomainException) { threw = true; }
    AssertTrue(threw, "Unbalanced journal entry must throw DomainException");

    var posted = await accSvc.CreateAndPostAsync(tId, DateTime.UtcNow, "Valid Sale", null, null,
        new List<JournalLineInput>
        {
            new(SystemAccountKeys.Cash, 250m, 0m),
            new(SystemAccountKeys.SalesRevenue, 0m, 250m)
        });
    AssertEqual(JournalEntryStatus.Posted, posted.Status, "Entry should be Posted");

    var rev = await accSvc.ReverseEntryAsync(tId, posted.Id, "Correction");
    AssertEqual(JournalEntryStatus.Reversed, posted.Status, "Original entry should be marked Reversed");
    AssertEqual(250m, rev.Lines.Sum(l => l.Credit), "Reversal credit sum should equal 250");
});

await RunTest("3. Inventory Weighted Average & Negative Stock Guard", async () =>
{
    var tId = Guid.NewGuid();
    await using var db = CreateDb(tId);
    var branch = new Branch { TenantId = tId, Code = "B1", Name = "Main" };
    var wh = new Warehouse { TenantId = tId, BranchId = branch.Id, Code = "W1", Name = "WH" };
    var cat = new Category { TenantId = tId, Code = "C1", Name = "Cat" };
    var uom = new UnitOfMeasure { TenantId = tId, Code = "KG", Name = "Kg" };
    var rm = new RawMaterial { TenantId = tId, Code = "RM-WA", Name = "Steel", CategoryId = cat.Id, UnitOfMeasureId = uom.Id };
    db.Tenants.Add(new Tenant { Id = tId, Code = "INVWA", Name = "WA Tenant", ValuationMethod = InventoryValuationMethod.WeightedAverage, AllowNegativeStock = false });
    db.Branches.Add(branch);
    db.Warehouses.Add(wh);
    db.Categories.Add(cat);
    db.UnitsOfMeasure.Add(uom);
    db.RawMaterials.Add(rm);
    await db.SaveChangesAsync();

    var inv = new InventoryService(db);
    await inv.ReceiveAsync(tId, wh.Id, rm.Id, null, 100m, 10m, StockTransactionType.PurchaseReceipt, "B1", null, null, null);
    await inv.ReceiveAsync(tId, wh.Id, rm.Id, null, 100m, 20m, StockTransactionType.PurchaseReceipt, "B2", null, null, null);

    var issue = await inv.IssueAsync(tId, wh.Id, rm.Id, null, 50m, StockTransactionType.ProductionIssue, null, null);
    AssertEqual(15m, issue.UnitCost, "Weighted Average unit cost should be 15.0000");
    AssertEqual(750m, issue.TotalCost, "Issued total cost should be 750.0000");

    bool blockedNegative = false;
    try
    {
        await inv.IssueAsync(tId, wh.Id, rm.Id, null, 500m, StockTransactionType.ProductionIssue, null, null);
    }
    catch (DomainException) { blockedNegative = true; }
    AssertTrue(blockedNegative, "Negative stock must be blocked when AllowNegativeStock is false");
});

await RunTest("4. Inventory FIFO Valuation Precision", async () =>
{
    var tId = Guid.NewGuid();
    await using var db = CreateDb(tId);
    var branch = new Branch { TenantId = tId, Code = "B1", Name = "Main" };
    var wh = new Warehouse { TenantId = tId, BranchId = branch.Id, Code = "W1", Name = "WH" };
    var cat = new Category { TenantId = tId, Code = "C1", Name = "Cat" };
    var uom = new UnitOfMeasure { TenantId = tId, Code = "KG", Name = "Kg" };
    var rm = new RawMaterial { TenantId = tId, Code = "RM-FIFO", Name = "Aluminum", CategoryId = cat.Id, UnitOfMeasureId = uom.Id };
    db.Tenants.Add(new Tenant { Id = tId, Code = "INVFIFO", Name = "FIFO Tenant", ValuationMethod = InventoryValuationMethod.FIFO });
    db.Branches.Add(branch);
    db.Warehouses.Add(wh);
    db.Categories.Add(cat);
    db.UnitsOfMeasure.Add(uom);
    db.RawMaterials.Add(rm);
    await db.SaveChangesAsync();

    var inv = new InventoryService(db);
    await inv.ReceiveAsync(tId, wh.Id, rm.Id, null, 100m, 10m, StockTransactionType.PurchaseReceipt, "F1", null, null, null);
    await Task.Delay(15);
    await inv.ReceiveAsync(tId, wh.Id, rm.Id, null, 100m, 20m, StockTransactionType.PurchaseReceipt, "F2", null, null, null);

    var issue = await inv.IssueAsync(tId, wh.Id, rm.Id, null, 120m, StockTransactionType.ProductionIssue, null, null);
    AssertEqual(1400m, issue.TotalCost, "FIFO 100@10 + 20@20 should equal 1400.0000");
});

await RunTest("5. Password Hashing & Refresh Token Lifecycle", () =>
{
    var hash = BCrypt.Net.BCrypt.HashPassword("Admin@123456");
    AssertTrue(BCrypt.Net.BCrypt.Verify("Admin@123456", hash), "Valid password should verify");
    AssertTrue(!BCrypt.Net.BCrypt.Verify("WrongPass", hash), "Invalid password should fail");

    var rt = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddDays(7) };
    AssertTrue(rt.IsActive, "New refresh token should be active");
    rt.RevokedAt = DateTime.UtcNow;
    AssertTrue(!rt.IsActive, "Revoked refresh token should not be active");
    return Task.CompletedTask;
});

Console.WriteLine($"\nSummary: {passed} passed, {failed} failed.");
return failed > 0 ? 1 : 0;

sealed class TestCurrentUserService : ICurrentUserService
{
    public TestCurrentUserService(Guid? tenantId) { TenantId = tenantId; }
    public Guid? UserId => Guid.Empty;
    public Guid? TenantId { get; }
    public string? Email => "test@factoryflow.demo";
    public string? IpAddress => "127.0.0.1";
    public IReadOnlyList<string> Roles => new[] { DefaultRoles.CompanyOwner };
    public bool IsAuthenticated => TenantId.HasValue;
}


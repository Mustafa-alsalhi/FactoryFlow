using FactoryFlow.Application.Common;
using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using FactoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db, IInventoryService inventory, IAccountingService accounting)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync())
            return;

        var tenant = new Tenant
        {
            Code = "DEMO",
            Name = "FactoryFlow Demo Manufacturing Co.",
            NameAr = "شركة فلو للصناعات النموذجية",
            Address = "Industrial Zone 3, Riyadh",
            Phone = "+966500000001",
            Email = "info@factoryflow.demo",
            TaxNumber = "300012345600003",
            BaseCurrencyCode = "USD",
            DecimalPlaces = 2,
            FiscalYearStartMonth = 1,
            ValuationMethod = InventoryValuationMethod.WeightedAverage,
            OverheadAllocationMethod = OverheadAllocationMethod.LaborHours,
            AllowNegativeStock = false,
            IsActive = true
        };
        db.Tenants.Add(tenant);

        var mainBranch = new Branch
        {
            TenantId = tenant.Id,
            Code = "MAIN",
            Name = "Main Factory Branch",
            NameAr = "الفرع الرئيسي للمصنع",
            Address = "Industrial Zone 3",
            IsMain = true
        };
        db.Branches.Add(mainBranch);

        var modules = new[]
        {
            "Dashboard", "RawMaterials", "Products", "BOM", "Manufacturing",
            "Costing", "Labor", "Machines", "Quality", "Warehouses",
            "Purchasing", "Sales", "Accounting", "Treasury", "Reports", "Settings", "AuditLogs"
        };

        var roles = new List<AppRole>();
        foreach (var roleName in DefaultRoles.All)
        {
            var role = new AppRole
            {
                TenantId = tenant.Id,
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant(),
                Description = $"{roleName} role"
            };
            roles.Add(role);
            db.Roles.Add(role);

            bool isFullAdmin = roleName is DefaultRoles.SuperAdmin or DefaultRoles.CompanyOwner or DefaultRoles.GeneralManager;
            foreach (var mod in modules)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    TenantId = tenant.Id,
                    RoleId = role.Id,
                    Module = mod,
                    CanView = isFullAdmin || CanRoleAccess(roleName, mod),
                    CanCreate = isFullAdmin || CanRoleWrite(roleName, mod),
                    CanEdit = isFullAdmin || CanRoleWrite(roleName, mod),
                    CanDelete = isFullAdmin,
                    CanApprove = isFullAdmin || roleName == DefaultRoles.Accountant && mod == "Accounting",
                    CanPrint = true,
                    CanExport = isFullAdmin || roleName == DefaultRoles.Accountant
                });
            }
        }

        var adminRole = roles.First(r => r.Name == DefaultRoles.CompanyOwner);
        var adminUser = new AppUser
        {
            TenantId = tenant.Id,
            BranchId = mainBranch.Id,
            Email = "admin@factoryflow.demo",
            NormalizedEmail = "ADMIN@FACTORYFLOW.DEMO",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
            FullName = "Ahmed Al-Mansoor",
            FullNameAr = "أحمد المنصور",
            PhoneNumber = "+966500000001",
            IsActive = true
        };
        db.Users.Add(adminUser);
        db.UserRoles.Add(new UserRole { TenantId = tenant.Id, UserId = adminUser.Id, RoleId = adminRole.Id });

        // Currencies & Taxes
        db.Currencies.AddRange(
            new Currency { TenantId = tenant.Id, Code = "USD", Name = "US Dollar", Symbol = "$", IsBase = true },
            new Currency { TenantId = tenant.Id, Code = "SAR", Name = "Saudi Riyal", Symbol = "ر.س", IsBase = false },
            new Currency { TenantId = tenant.Id, Code = "EUR", Name = "Euro", Symbol = "€", IsBase = false }
        );
        db.ExchangeRates.Add(new ExchangeRate { TenantId = tenant.Id, FromCurrencyCode = "USD", ToCurrencyCode = "SAR", Rate = 3.75m });
        db.TaxRates.AddRange(
            new TaxRate { TenantId = tenant.Id, Code = "VAT15", Name = "Standard VAT 15%", NameAr = "ضريبة القيمة المضافة 15%", RatePercentage = 15m, IsDefault = true },
            new TaxRate { TenantId = tenant.Id, Code = "EXEMPT", Name = "Tax Exempt 0%", NameAr = "معفى من الضريبة 0%", RatePercentage = 0m, IsExempt = true }
        );

        // Chart of Accounts
        var accounts = SeedChartOfAccounts(tenant.Id);
        db.Accounts.AddRange(accounts);

        var cashAcc = accounts.First(a => a.SystemKey == SystemAccountKeys.Cash);
        var bankAcc = accounts.First(a => a.SystemKey == SystemAccountKeys.Bank);
        db.Cashboxes.Add(new Cashbox { TenantId = tenant.Id, BranchId = mainBranch.Id, Code = "CB-01", Name = "Main Cashbox", NameAr = "الصندوق الرئيسي", CurrencyCode = "USD", CurrentBalance = 50000m, AccountId = cashAcc.Id });
        db.BankAccounts.Add(new BankAccount { TenantId = tenant.Id, Code = "BNK-01", BankName = "Al Rajhi Corporate Bank", AccountNumber = "SA001234567890", CurrencyCode = "USD", CurrentBalance = 150000m, AccountId = bankAcc.Id });

        // Units & Categories
        var uomKg = new UnitOfMeasure { TenantId = tenant.Id, Code = "KG", Name = "Kilogram", NameAr = "كيلوجرام" };
        var uomPcs = new UnitOfMeasure { TenantId = tenant.Id, Code = "PCS", Name = "Piece", NameAr = "قطعة" };
        var uomL = new UnitOfMeasure { TenantId = tenant.Id, Code = "L", Name = "Liter", NameAr = "لتر" };
        db.UnitsOfMeasure.AddRange(uomKg, uomPcs, uomL);

        var catRaw = new Category { TenantId = tenant.Id, Code = "CAT-RM", Name = "Raw Ingredients & Metals", NameAr = "مواد أولية ومعادن", CategoryFor = "RawMaterial" };
        var catProd = new Category { TenantId = tenant.Id, Code = "CAT-FG", Name = "Finished Goods", NameAr = "منتجات نهائية", CategoryFor = "Product" };
        db.Categories.AddRange(catRaw, catProd);

        var whRaw = new Warehouse { TenantId = tenant.Id, BranchId = mainBranch.Id, Code = "WH-RM", Name = "Raw Materials Warehouse", NameAr = "مستودع المواد الخام", Location = "Building A" };
        var whFg = new Warehouse { TenantId = tenant.Id, BranchId = mainBranch.Id, Code = "WH-FG", Name = "Finished Goods Warehouse", NameAr = "مستودع المنتجات النهائية", Location = "Building B" };
        db.Warehouses.AddRange(whRaw, whFg);

        var sup1 = new Supplier { TenantId = tenant.Id, Code = "SUP-01", Name = "Global Industrial Supplies", NameAr = "الشركة العالمية للتوريدات", Phone = "+966501112233", Email = "sales@globalsupplies.com" };
        var cust1 = new Customer { TenantId = tenant.Id, Code = "CUST-01", Name = "Al-Noor Hypermarkets", NameAr = "أسواق النور", Phone = "+966504445566", Email = "orders@alnoor.com", CreditLimit = 100000m };
        db.Suppliers.Add(sup1);
        db.Customers.Add(cust1);

        var rm1 = new RawMaterial { TenantId = tenant.Id, Code = "RM-101", Name = "Industrial Flour Grade A", NameAr = "دقيق صناعي فاخر", CategoryId = catRaw.Id, UnitOfMeasureId = uomKg.Id, PreferredSupplierId = sup1.Id, StandardCost = 2.50m, LastPurchasePrice = 2.50m, MinimumStockLevel = 200m };
        var rm2 = new RawMaterial { TenantId = tenant.Id, Code = "RM-102", Name = "Refined Sugar", NameAr = "سكر مكرر", CategoryId = catRaw.Id, UnitOfMeasureId = uomKg.Id, PreferredSupplierId = sup1.Id, StandardCost = 1.80m, LastPurchasePrice = 1.80m, MinimumStockLevel = 150m };
        var rm3 = new RawMaterial { TenantId = tenant.Id, Code = "RM-103", Name = "Packaging Box", NameAr = "عبوة تغليف كرتون", CategoryId = catRaw.Id, UnitOfMeasureId = uomPcs.Id, PreferredSupplierId = sup1.Id, StandardCost = 0.50m, LastPurchasePrice = 0.50m, MinimumStockLevel = 500m };
        db.RawMaterials.AddRange(rm1, rm2, rm3);

        var prod1 = new Product { TenantId = tenant.Id, Sku = "FG-201", Barcode = "6281000002011", Name = "Premium Bakery Mix 10KG", NameAr = "خلطة مخبوزات فاخرة 10 كجم", CategoryId = catProd.Id, UnitOfMeasureId = uomPcs.Id, SellingPrice = 45.00m, StandardCost = 28.50m, MinimumStockLevel = 50m };
        db.Products.Add(prod1);

        var bom1 = new BillOfMaterials
        {
            TenantId = tenant.Id,
            ProductId = prod1.Id,
            Code = "BOM-201-V1",
            Version = 1,
            OutputQuantity = 1m,
            EstimatedMaterialCost = 24.40m,
            EstimatedLaborCost = 2.50m,
            EstimatedOverheadCost = 1.60m,
            Items = new List<BomItem>
            {
                new() { TenantId = tenant.Id, RawMaterialId = rm1.Id, UnitOfMeasureId = uomKg.Id, Quantity = 7m, WastePercentage = 2m, UnitCost = 2.50m, TotalCost = 17.85m },
                new() { TenantId = tenant.Id, RawMaterialId = rm2.Id, UnitOfMeasureId = uomKg.Id, Quantity = 3m, WastePercentage = 1m, UnitCost = 1.80m, TotalCost = 5.45m },
                new() { TenantId = tenant.Id, RawMaterialId = rm3.Id, UnitOfMeasureId = uomPcs.Id, Quantity = 1m, WastePercentage = 0m, UnitCost = 0.50m, TotalCost = 0.50m }
            }
        };
        db.BillsOfMaterials.Add(bom1);

        var line1 = new ProductionLine { TenantId = tenant.Id, BranchId = mainBranch.Id, Code = "LINE-01", Name = "Automated Mixing Line 1", NameAr = "خط الخلط الآلي 1", DailyCapacityHours = 16m };
        db.ProductionLines.Add(line1);

        var machine1 = new Machine { TenantId = tenant.Id, ProductionLineId = line1.Id, Code = "MCH-01", Name = "High-Speed Mixer 500L", NameAr = "خلاط صناعي سريع 500 لتر", HourlyOperatingCost = 12m, PurchaseCost = 45000m, UsefulLifeHours = 10000m, NextMaintenanceDate = DateTime.UtcNow.AddDays(15) };
        db.Machines.Add(machine1);

        var emp1 = new Employee { TenantId = tenant.Id, BranchId = mainBranch.Id, Code = "EMP-01", FullName = "Khaled Hassan", FullNameAr = "خالد حسن", Department = "Production", JobTitle = "Line Supervisor", WageType = WageType.Hourly, BaseWageRate = 18m, OvertimeHourlyRate = 27m };
        db.Employees.Add(emp1);

        await db.SaveChangesAsync();

        // Post Opening Balances (Cash + Bank + Raw Materials Inventory = Capital/Retained Earnings)
        await inventory.ReceiveAsync(tenant.Id, whRaw.Id, rm1.Id, null, 1000m, 2.50m, StockTransactionType.AdjustmentIn, "OPEN-RM1", null, "OpeningBalance", null, "Initial stock");
        await inventory.ReceiveAsync(tenant.Id, whRaw.Id, rm2.Id, null, 800m, 1.80m, StockTransactionType.AdjustmentIn, "OPEN-RM2", null, "OpeningBalance", null, "Initial stock");
        await inventory.ReceiveAsync(tenant.Id, whRaw.Id, rm3.Id, null, 2000m, 0.50m, StockTransactionType.AdjustmentIn, "OPEN-RM3", null, "OpeningBalance", null, "Initial stock");

        decimal totalOpeningInv = (1000m * 2.50m) + (800m * 1.80m) + (2000m * 0.50m); // 2500 + 1440 + 1000 = 4940
        decimal totalCapital = 50000m + 150000m + totalOpeningInv; // 204,940

        await accounting.CreateAndPostAsync(
            tenant.Id,
            DateTime.UtcNow.AddDays(-7),
            "Opening Balances Entry",
            "OpeningBalance",
            tenant.Id,
            new List<JournalLineInput>
            {
                new(SystemAccountKeys.Cash, 50000m, 0m, "Opening Cashbox"),
                new(SystemAccountKeys.Bank, 150000m, 0m, "Opening Bank"),
                new(SystemAccountKeys.RawMaterialInventory, totalOpeningInv, 0m, "Opening Raw Materials"),
                new(SystemAccountKeys.RetainedEarnings, 0m, totalCapital, "Opening Equity / Capital")
            },
            $"OPEN-{tenant.Id}");
    }

    private static bool CanRoleAccess(string role, string mod) => (role, mod) switch
    {
        (DefaultRoles.Accountant, "Accounting" or "Treasury" or "Reports" or "Dashboard" or "Costing") => true,
        (DefaultRoles.ProductionManager, "Manufacturing" or "BOM" or "Products" or "Machines" or "Labor" or "Costing" or "Dashboard") => true,
        (DefaultRoles.WarehouseManager, "Warehouses" or "RawMaterials" or "Products" or "Dashboard") => true,
        (DefaultRoles.PurchasingManager, "Purchasing" or "RawMaterials" or "Dashboard") => true,
        (DefaultRoles.SalesManager, "Sales" or "Products" or "Dashboard") => true,
        (DefaultRoles.QualityController, "Quality" or "Manufacturing" or "Dashboard") => true,
        _ => mod == "Dashboard"
    };

    private static bool CanRoleWrite(string role, string mod) => (role, mod) switch
    {
        (DefaultRoles.Accountant, "Accounting" or "Treasury") => true,
        (DefaultRoles.ProductionManager, "Manufacturing" or "BOM" or "Machines" or "Labor") => true,
        (DefaultRoles.WarehouseManager, "Warehouses" or "RawMaterials") => true,
        (DefaultRoles.PurchasingManager, "Purchasing") => true,
        (DefaultRoles.SalesManager, "Sales") => true,
        (DefaultRoles.QualityController, "Quality") => true,
        _ => false
    };

    private static List<Account> SeedChartOfAccounts(Guid tenantId) => new()
    {
        new() { TenantId = tenantId, Code = "1010", Name = "Cash on Hand", NameAr = "النقدية بالصندوق", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.Cash },
        new() { TenantId = tenantId, Code = "1020", Name = "Bank Accounts", NameAr = "الحسابات البنكية", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.Bank },
        new() { TenantId = tenantId, Code = "1100", Name = "Accounts Receivable", NameAr = "ذمم العملاء", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.AccountsReceivable },
        new() { TenantId = tenantId, Code = "1210", Name = "Raw Materials Inventory", NameAr = "مخزون المواد الخام", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.RawMaterialInventory },
        new() { TenantId = tenantId, Code = "1220", Name = "Work In Progress (WIP)", NameAr = "الإنتاج تحت التشغيل", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.WorkInProgress },
        new() { TenantId = tenantId, Code = "1230", Name = "Finished Goods Inventory", NameAr = "مخزون المنتجات النهائية", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.FinishedGoodsInventory },
        new() { TenantId = tenantId, Code = "1300", Name = "VAT Receivable (Input)", NameAr = "ضريبة المدخلات", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.TaxReceivable },
        new() { TenantId = tenantId, Code = "2010", Name = "Accounts Payable", NameAr = "ذمم الموردين", AccountType = AccountType.Liability, IsSystemAccount = true, SystemKey = SystemAccountKeys.AccountsPayable },
        new() { TenantId = tenantId, Code = "2200", Name = "VAT Payable (Output)", NameAr = "ضريبة المخرجات", AccountType = AccountType.Liability, IsSystemAccount = true, SystemKey = SystemAccountKeys.TaxPayable },
        new() { TenantId = tenantId, Code = "3010", Name = "Owner Capital & Retained Earnings", NameAr = "رأس المال والأرباح المبقاة", AccountType = AccountType.Equity, IsSystemAccount = true, SystemKey = SystemAccountKeys.RetainedEarnings },
        new() { TenantId = tenantId, Code = "4010", Name = "Sales Revenue", NameAr = "إيرادات المبيعات", AccountType = AccountType.Revenue, IsSystemAccount = true, SystemKey = SystemAccountKeys.SalesRevenue },
        new() { TenantId = tenantId, Code = "5010", Name = "Cost of Goods Sold (COGS)", NameAr = "تكلفة البضاعة المباعة", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.CostOfGoodsSold },
        new() { TenantId = tenantId, Code = "5100", Name = "Direct Labor & Wages", NameAr = "أجور عمال الإنتاج", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.DirectLaborExpense },
        new() { TenantId = tenantId, Code = "5200", Name = "Manufacturing Overhead", NameAr = "التكاليف الصناعية غير المباشرة", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.ManufacturingOverhead },
        new() { TenantId = tenantId, Code = "5300", Name = "Scrap & Spoilage Loss", NameAr = "خسائر التلف والهدر", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.ScrapLoss },
        new() { TenantId = tenantId, Code = "5400", Name = "Inventory Adjustment", NameAr = "فروقات جرد المخزون", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.InventoryAdjustment }
    };
}


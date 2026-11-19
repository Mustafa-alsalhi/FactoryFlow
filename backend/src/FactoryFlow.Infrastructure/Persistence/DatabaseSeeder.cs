using FactoryFlow.Application.Common;
using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db, IInventoryService inventory, IAccountingService accounting)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Code == "DEMO");
        if (tenant == null)
        {
            tenant = new Tenant
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
                        CanApprove = isFullAdmin || (roleName == DefaultRoles.Accountant && mod == "Accounting"),
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

            var accounts = SeedChartOfAccounts(tenant.Id);
            db.Accounts.AddRange(accounts);

            var cashAcc = accounts.First(a => a.SystemKey == SystemAccountKeys.Cash);
            var bankAcc = accounts.First(a => a.SystemKey == SystemAccountKeys.Bank);
            db.Cashboxes.Add(new Cashbox { TenantId = tenant.Id, BranchId = mainBranch.Id, Code = "CB-01", Name = "Main Cashbox", NameAr = "الصندوق الرئيسي", CurrencyCode = "USD", CurrentBalance = 50000m, AccountId = cashAcc.Id });
            db.BankAccounts.Add(new BankAccount { TenantId = tenant.Id, Code = "BNK-01", BankName = "Al Rajhi Corporate Bank", AccountNumber = "SA001234567890", CurrencyCode = "USD", CurrentBalance = 150000m, AccountId = bankAcc.Id });

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
                EstimatedMaterialCost = 23.80m,
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

            var machine1 = new Machine { TenantId = tenant.Id, ProductionLineId = line1.Id, Code = "MCH-01", Name = "High-Speed Mixer 500L", NameAr = "خلاط صناعي سريع 500 لتر", HourlyOperatingCost = 12m, PurchaseCost = 45000m, UsefulLifeHours = 10000m, TotalRunHours = 120m, NextMaintenanceDate = DateTime.UtcNow.AddDays(15) };
            db.Machines.Add(machine1);

            var emp1 = new Employee { TenantId = tenant.Id, BranchId = mainBranch.Id, Code = "EMP-01", FullName = "Khaled Hassan", FullNameAr = "خالد حسن", Department = "Production", JobTitle = "Line Supervisor", WageType = WageType.Hourly, BaseWageRate = 18m, OvertimeHourlyRate = 27m };
            db.Employees.Add(emp1);

            await db.SaveChangesAsync();

            await inventory.ReceiveAsync(tenant.Id, whRaw.Id, rm1.Id, null, 1000m, 2.50m, StockTransactionType.AdjustmentIn, "OPEN-RM1", null, "OpeningBalance", null, "Initial stock");
            await inventory.ReceiveAsync(tenant.Id, whRaw.Id, rm2.Id, null, 800m, 1.80m, StockTransactionType.AdjustmentIn, "OPEN-RM2", null, "OpeningBalance", null, "Initial stock");
            await inventory.ReceiveAsync(tenant.Id, whRaw.Id, rm3.Id, null, 2000m, 0.50m, StockTransactionType.AdjustmentIn, "OPEN-RM3", null, "OpeningBalance", null, "Initial stock");

            decimal totalOpeningInv = (1000m * 2.50m) + (800m * 1.80m) + (2000m * 0.50m);
            decimal totalCapital = 50000m + 150000m + totalOpeningInv;

            await accounting.CreateAndPostAsync(
                tenant.Id,
                DateTime.UtcNow.AddDays(-30),
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

        // Seed realistic operational transactions (Production Orders, Purchasing, Sales, QC, Treasury) if not yet seeded
        if (!await db.ProductionOrders.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenant.Id))
        {
            await SeedOperationalDemoDataAsync(db, inventory, accounting, tenant.Id);
        }
    }

    private static async Task SeedOperationalDemoDataAsync(AppDbContext db, IInventoryService inventory, IAccountingService accounting, Guid tenantId)
    {
        var branch = await db.Branches.IgnoreQueryFilters().FirstAsync(b => b.TenantId == tenantId);
        var warehouses = await db.Warehouses.IgnoreQueryFilters().Where(w => w.TenantId == tenantId).ToListAsync();
        var whRaw = warehouses.First(w => w.Code == "WH-RM");
        var whFg = warehouses.FirstOrDefault(w => w.Code == "WH-FG") ?? whRaw;
        var supplier = await db.Suppliers.IgnoreQueryFilters().FirstAsync(s => s.TenantId == tenantId);
        var customer = await db.Customers.IgnoreQueryFilters().FirstAsync(c => c.TenantId == tenantId);
        var rawMaterials = await db.RawMaterials.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ToListAsync();
        var rm1 = rawMaterials.First(r => r.Code == "RM-101");
        var rm2 = rawMaterials.First(r => r.Code == "RM-102");
        var rm3 = rawMaterials.First(r => r.Code == "RM-103");
        var product = await db.Products.IgnoreQueryFilters().FirstAsync(p => p.TenantId == tenantId);
        var bom = await db.BillsOfMaterials.IgnoreQueryFilters().FirstAsync(b => b.TenantId == tenantId);
        var line = await db.ProductionLines.IgnoreQueryFilters().FirstAsync(l => l.TenantId == tenantId);
        var machine = await db.Machines.IgnoreQueryFilters().FirstAsync(m => m.TenantId == tenantId);
        var emp = await db.Employees.IgnoreQueryFilters().FirstAsync(e => e.TenantId == tenantId);
        var cashbox = await db.Cashboxes.IgnoreQueryFilters().FirstAsync(c => c.TenantId == tenantId);

        // 1. Additional Raw Material, Finished Product, Supplier, Customer & Employee
        var catRaw = await db.Categories.IgnoreQueryFilters().FirstAsync(c => c.TenantId == tenantId && c.CategoryFor == "RawMaterial");
        var catProd = await db.Categories.IgnoreQueryFilters().FirstAsync(c => c.TenantId == tenantId && c.CategoryFor == "Product");
        var uomKg = await db.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync(u => u.TenantId == tenantId && u.Code == "KG");
        var uomPcs = await db.UnitsOfMeasure.IgnoreQueryFilters().FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");

        var rm4 = await db.RawMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code == "RM-104");
        if (rm4 == null)
        {
            rm4 = new RawMaterial { TenantId = tenantId, Code = "RM-104", Name = "Food-Grade Yeast", NameAr = "خميرة غذائية فورية", CategoryId = catRaw.Id, UnitOfMeasureId = uomKg.Id, PreferredSupplierId = supplier.Id, StandardCost = 6.20m, LastPurchasePrice = 6.20m, MinimumStockLevel = 80m };
            db.RawMaterials.Add(rm4);
        }

        var prod2 = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Sku == "FG-202");
        if (prod2 == null)
        {
            prod2 = new Product { TenantId = tenantId, Sku = "FG-202", Barcode = "6281000002028", Name = "Artisan Pastry Flour 5KG", NameAr = "دقيق معجنات فاخر 5 كجم", CategoryId = catProd.Id, UnitOfMeasureId = uomPcs.Id, SellingPrice = 26.00m, StandardCost = 16.40m, MinimumStockLevel = 40m };
            db.Products.Add(prod2);
        }

        if (!await db.Suppliers.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId && s.Code == "SUP-02"))
            db.Suppliers.Add(new Supplier { TenantId = tenantId, Code = "SUP-02", Name = "Al-Faisal Packaging Co.", NameAr = "شركة الفيصل للتغليف", Phone = "+966509988776", Email = "orders@alfaisalpack.com" });

        var cust2 = await db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "CUST-02");
        if (cust2 == null)
        {
            cust2 = new Customer { TenantId = tenantId, Code = "CUST-02", Name = "Golden Bakery Chain", NameAr = "سلسلة المخابز الذهبية", Phone = "+966553322110", Email = "procurement@goldenbakery.com", CreditLimit = 75000m };
            db.Customers.Add(cust2);
        }

        var emp2 = await db.Employees.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Code == "EMP-02");
        if (emp2 == null)
        {
            emp2 = new Employee { TenantId = tenantId, BranchId = branch.Id, Code = "EMP-02", FullName = "Omar Al-Harbi", FullNameAr = "عمر الحربي", Department = "Production", JobTitle = "Machine Operator", WageType = WageType.Hourly, BaseWageRate = 14m, OvertimeHourlyRate = 21m };
            db.Employees.Add(emp2);
        }
        await db.SaveChangesAsync();

        // Low-stock receipt for RM-104 (50 < Min 80) so Low Stock alert is real
        await inventory.ReceiveAsync(tenantId, whRaw.Id, rm4.Id, null, 50m, 6.20m, StockTransactionType.PurchaseReceipt, "B-RM4-01", DateTime.UtcNow.AddMonths(8), "Seed", null, "Initial yeast batch");

        // 2. Posted Purchase Invoice (500 KG Flour + 300 KG Sugar)
        var pi = new PurchaseInvoice
        {
            TenantId = tenantId,
            InvoiceNumber = "PI-20260001",
            SupplierId = supplier.Id,
            WarehouseId = whRaw.Id,
            InvoiceDate = DateTime.UtcNow.AddDays(-20),
            Status = InvoiceStatus.Posted,
            SubTotal = 1790m,
            TaxAmount = 268.50m,
            TotalAmount = 2058.50m,
            Items = new List<PurchaseInvoiceItem>
            {
                new() { TenantId = tenantId, RawMaterialId = rm1.Id, BatchNumber = "FLR-26A", Quantity = 500m, UnitPrice = 2.50m, TaxAmount = 187.50m, LineTotal = 1437.50m },
                new() { TenantId = tenantId, RawMaterialId = rm2.Id, BatchNumber = "SGR-26A", Quantity = 300m, UnitPrice = 1.80m, TaxAmount = 81.00m, LineTotal = 621.00m }
            }
        };
        await inventory.ReceiveAsync(tenantId, whRaw.Id, rm1.Id, null, 500m, 2.50m, StockTransactionType.PurchaseReceipt, "FLR-26A", null, nameof(PurchaseInvoice), pi.Id, "Purchase PI-20260001");
        await inventory.ReceiveAsync(tenantId, whRaw.Id, rm2.Id, null, 300m, 1.80m, StockTransactionType.PurchaseReceipt, "SGR-26A", null, nameof(PurchaseInvoice), pi.Id, "Purchase PI-20260001");
        supplier.Balance += 2058.50m;
        var piJe = await accounting.CreateAndPostAsync(tenantId, pi.InvoiceDate, "Purchase Invoice PI-20260001", nameof(PurchaseInvoice), pi.Id, new List<JournalLineInput>
        {
            new(SystemAccountKeys.RawMaterialInventory, 1790m, 0m, "Raw Materials Receipt"),
            new(SystemAccountKeys.TaxReceivable, 268.50m, 0m, "Input VAT 15%"),
            new(SystemAccountKeys.AccountsPayable, 0m, 2058.50m, supplier.Name)
        }, $"SEED-PI-{pi.Id}");
        pi.JournalEntryId = piJe.Id;
        db.PurchaseInvoices.Add(pi);

        // 3. Completed Production Order (100 units of FG-201)
        var mo1 = new ProductionOrder
        {
            TenantId = tenantId,
            OrderNumber = "MO-20260001",
            BranchId = branch.Id,
            WarehouseId = whRaw.Id,
            OutputWarehouseId = whFg.Id,
            ProductId = product.Id,
            BomId = bom.Id,
            ProductionLineId = line.Id,
            SupervisorId = emp.Id,
            PlannedQuantity = 100m,
            ProducedQuantity = 98m,
            DefectiveQuantity = 2m,
            PlannedStartDate = DateTime.UtcNow.AddDays(-14),
            PlannedEndDate = DateTime.UtcNow.AddDays(-12),
            ActualStartDate = DateTime.UtcNow.AddDays(-14),
            ActualEndDate = DateTime.UtcNow.AddDays(-12),
            Status = ProductionOrderStatus.Completed,
            PlannedMaterialCost = 2380m,
            PlannedLaborCost = 250m,
            PlannedOverheadCost = 160m
        };

        var txMat1 = await inventory.IssueAsync(tenantId, whRaw.Id, rm1.Id, null, 710m, StockTransactionType.ProductionIssue, nameof(ProductionOrder), mo1.Id, "MO-20260001 Flour");
        var txMat2 = await inventory.IssueAsync(tenantId, whRaw.Id, rm2.Id, null, 305m, StockTransactionType.ProductionIssue, nameof(ProductionOrder), mo1.Id, "MO-20260001 Sugar");
        var txMat3 = await inventory.IssueAsync(tenantId, whRaw.Id, rm3.Id, null, 100m, StockTransactionType.ProductionIssue, nameof(ProductionOrder), mo1.Id, "MO-20260001 Boxes");
        decimal actualMat = txMat1.TotalCost + txMat2.TotalCost + txMat3.TotalCost;
        decimal actualLab = 264m;
        decimal actualOvh = 175m;
        decimal totalWip = actualMat + actualLab + actualOvh;
        decimal defectCost = Math.Round(totalWip * (2m / 100m), 4);
        decimal goodCost = totalWip - defectCost;
        decimal goodUnitCost = Math.Round(goodCost / 98m, 4);

        mo1.ActualMaterialCost = actualMat;
        mo1.ActualLaborCost = actualLab;
        mo1.ActualOverheadCost = actualOvh;
        mo1.ScrapAndWasteCost = defectCost;
        mo1.ActualUnitCost = goodUnitCost;
        product.StandardCost = goodUnitCost;

        mo1.MaterialIssues.Add(new ProductionMaterialIssue { TenantId = tenantId, ProductionOrderId = mo1.Id, RawMaterialId = rm1.Id, WarehouseId = whRaw.Id, PlannedQuantity = 714m, IssuedQuantity = 710m, ConsumedQuantity = 710m, UnitCost = txMat1.UnitCost, TotalCost = txMat1.TotalCost });
        mo1.MaterialIssues.Add(new ProductionMaterialIssue { TenantId = tenantId, ProductionOrderId = mo1.Id, RawMaterialId = rm2.Id, WarehouseId = whRaw.Id, PlannedQuantity = 303m, IssuedQuantity = 305m, ConsumedQuantity = 305m, UnitCost = txMat2.UnitCost, TotalCost = txMat2.TotalCost });
        mo1.MaterialIssues.Add(new ProductionMaterialIssue { TenantId = tenantId, ProductionOrderId = mo1.Id, RawMaterialId = rm3.Id, WarehouseId = whRaw.Id, PlannedQuantity = 100m, IssuedQuantity = 100m, ConsumedQuantity = 100m, UnitCost = txMat3.UnitCost, TotalCost = txMat3.TotalCost });
        mo1.LaborLogs.Add(new ProductionLaborLog { TenantId = tenantId, ProductionOrderId = mo1.Id, EmployeeId = emp.Id, WorkDate = DateTime.UtcNow.AddDays(-13), RegularHours = 12m, OvertimeHours = 0m, Bonuses = 48m, TotalLaborCost = actualLab });
        mo1.OverheadLogs.Add(new ProductionOverheadLog { TenantId = tenantId, ProductionOrderId = mo1.Id, MachineId = machine.Id, Category = "Electricity", MachineHours = 10m, Amount = actualOvh, Notes = "Mixing line power & depreciation" });
        mo1.Outputs.Add(new ProductionOutput { TenantId = tenantId, ProductionOrderId = mo1.Id, OutputDate = DateTime.UtcNow.AddDays(-12), GoodQuantity = 98m, DefectiveQuantity = 2m, BatchNumber = "FG-BATCH-01", UnitCost = goodUnitCost, TotalCost = goodCost });
        db.ProductionOrders.Add(mo1);

        await inventory.ReceiveAsync(tenantId, whFg.Id, null, product.Id, 98m, goodUnitCost, StockTransactionType.ProductionOutput, "FG-BATCH-01", null, nameof(ProductionOrder), mo1.Id, "Completed MO-20260001");
        await inventory.ReceiveAsync(tenantId, whFg.Id, null, prod2.Id, 80m, 16.40m, StockTransactionType.AdjustmentIn, "FG2-OPEN", null, "OpeningBalance", null, "Initial FG-202 stock");

        await accounting.CreateAndPostAsync(tenantId, DateTime.UtcNow.AddDays(-13), "WIP Accumulation MO-20260001", nameof(ProductionOrder), mo1.Id, new List<JournalLineInput>
        {
            new(SystemAccountKeys.WorkInProgress, totalWip, 0m, "WIP Materials, Labor & Overhead"),
            new(SystemAccountKeys.RawMaterialInventory, 0m, actualMat, "Raw Materials Issued"),
            new(SystemAccountKeys.DirectLaborExpense, 0m, actualLab, "Direct Labor Applied"),
            new(SystemAccountKeys.ManufacturingOverhead, 0m, actualOvh, "Overhead Applied")
        }, $"SEED-MO1-WIP-{mo1.Id}");

        await accounting.CreateAndPostAsync(tenantId, DateTime.UtcNow.AddDays(-12), "Finished Goods Completion MO-20260001", nameof(ProductionOrder), mo1.Id, new List<JournalLineInput>
        {
            new(SystemAccountKeys.FinishedGoodsInventory, goodCost, 0m, "98 units to FG Warehouse"),
            new(SystemAccountKeys.ScrapLoss, defectCost, 0m, "2 defective units scrap"),
            new(SystemAccountKeys.WorkInProgress, 0m, totalWip, "Clear WIP MO-20260001")
        }, $"SEED-MO1-FIN-{mo1.Id}");

        // 4. Active InProgress Production Order (MO-20260002)
        var mo2 = new ProductionOrder
        {
            TenantId = tenantId,
            OrderNumber = "MO-20260002",
            BranchId = branch.Id,
            WarehouseId = whRaw.Id,
            OutputWarehouseId = whFg.Id,
            ProductId = product.Id,
            BomId = bom.Id,
            ProductionLineId = line.Id,
            SupervisorId = emp2.Id,
            PlannedQuantity = 60m,
            PlannedStartDate = DateTime.UtcNow.AddDays(-1),
            PlannedEndDate = DateTime.UtcNow.AddDays(2),
            ActualStartDate = DateTime.UtcNow.AddDays(-1),
            Status = ProductionOrderStatus.InProgress,
            PlannedMaterialCost = 1428m,
            PlannedLaborCost = 150m,
            PlannedOverheadCost = 96m
        };
        db.ProductionOrders.Add(mo2);

        // 5. Quality Inspection
        db.QualityInspections.Add(new QualityInspection
        {
            TenantId = tenantId,
            InspectionNumber = "QC-20260001",
            InspectionType = QualityInspectionType.FinishedProduct,
            ProductionOrderId = mo1.Id,
            ProductId = product.Id,
            InspectionDate = DateTime.UtcNow.AddDays(-12),
            InspectedQuantity = 100m,
            AcceptedQuantity = 98m,
            RejectedQuantity = 2m,
            DefectReason = "Torn outer packaging seal",
            Status = QualityInspectionStatus.Partial,
            InspectorName = "admin@factoryflow.demo"
        });

        // 6. Sales Invoices (Current Month + Last Month for multi-month chart)
        var saleTx1 = await inventory.IssueAsync(tenantId, whFg.Id, null, product.Id, 40m, StockTransactionType.SalesIssue, nameof(SalesInvoice), null, "Sale SI-20260001");
        var si1 = new SalesInvoice
        {
            TenantId = tenantId,
            InvoiceNumber = "SI-20260001",
            CustomerId = customer.Id,
            WarehouseId = whFg.Id,
            InvoiceDate = DateTime.UtcNow.AddMonths(-1).AddDays(5),
            Status = InvoiceStatus.Posted,
            SubTotal = 1800m,
            TaxAmount = 270m,
            TotalAmount = 2070m,
            TotalCogs = saleTx1.TotalCost,
            Items = new List<SalesInvoiceItem>
            {
                new() { TenantId = tenantId, ProductId = product.Id, Quantity = 40m, UnitPrice = 45m, UnitCost = saleTx1.UnitCost, TaxAmount = 270m, LineTotal = 2070m }
            }
        };
        customer.Balance += 2070m;
        var si1Je = await accounting.CreateAndPostAsync(tenantId, si1.InvoiceDate, "Sales Invoice SI-20260001", nameof(SalesInvoice), si1.Id, new List<JournalLineInput>
        {
            new(SystemAccountKeys.AccountsReceivable, 2070m, 0m, customer.Name),
            new(SystemAccountKeys.SalesRevenue, 0m, 1800m, "Sales Revenue"),
            new(SystemAccountKeys.TaxPayable, 0m, 270m, "Output VAT 15%"),
            new(SystemAccountKeys.CostOfGoodsSold, saleTx1.TotalCost, 0m, "COGS SI-20260001"),
            new(SystemAccountKeys.FinishedGoodsInventory, 0m, saleTx1.TotalCost, "FG Issue")
        }, $"SEED-SI1-{si1.Id}");
        si1.JournalEntryId = si1Je.Id;
        db.SalesInvoices.Add(si1);

        var saleTx2 = await inventory.IssueAsync(tenantId, whFg.Id, null, prod2.Id, 50m, StockTransactionType.SalesIssue, nameof(SalesInvoice), null, "Sale SI-20260002");
        var si2 = new SalesInvoice
        {
            TenantId = tenantId,
            InvoiceNumber = "SI-20260002",
            CustomerId = cust2.Id,
            WarehouseId = whFg.Id,
            InvoiceDate = DateTime.UtcNow.AddDays(-3),
            Status = InvoiceStatus.Paid,
            IsCashSale = true,
            SubTotal = 1300m,
            TaxAmount = 195m,
            TotalAmount = 1495m,
            PaidAmount = 1495m,
            TotalCogs = saleTx2.TotalCost,
            Items = new List<SalesInvoiceItem>
            {
                new() { TenantId = tenantId, ProductId = prod2.Id, Quantity = 50m, UnitPrice = 26m, UnitCost = saleTx2.UnitCost, TaxAmount = 195m, LineTotal = 1495m }
            }
        };
        cashbox.CurrentBalance += 1495m;
        var si2Je = await accounting.CreateAndPostAsync(tenantId, si2.InvoiceDate, "Cash Sales Invoice SI-20260002", nameof(SalesInvoice), si2.Id, new List<JournalLineInput>
        {
            new(SystemAccountKeys.Cash, 1495m, 0m, "Cash Receipt"),
            new(SystemAccountKeys.SalesRevenue, 0m, 1300m, "Sales Revenue"),
            new(SystemAccountKeys.TaxPayable, 0m, 195m, "Output VAT 15%"),
            new(SystemAccountKeys.CostOfGoodsSold, saleTx2.TotalCost, 0m, "COGS SI-20260002"),
            new(SystemAccountKeys.FinishedGoodsInventory, 0m, saleTx2.TotalCost, "FG Issue")
        }, $"SEED-SI2-{si2.Id}");
        si2.JournalEntryId = si2Je.Id;
        db.SalesInvoices.Add(si2);

        // 7. Customer Receipt Voucher
        var voucher = new TreasuryVoucher
        {
            TenantId = tenantId,
            VoucherNumber = "TR-20260001",
            VoucherType = VoucherType.Receipt,
            VoucherDate = DateTime.UtcNow.AddDays(-2),
            CashboxId = cashbox.Id,
            CustomerId = customer.Id,
            Amount = 1000m,
            Description = "Partial payment from Al-Noor Hypermarkets"
        };
        cashbox.CurrentBalance += 1000m;
        customer.Balance -= 1000m;
        var vJe = await accounting.CreateAndPostAsync(tenantId, voucher.VoucherDate, voucher.Description, nameof(TreasuryVoucher), voucher.Id, new List<JournalLineInput>
        {
            new(SystemAccountKeys.Cash, 1000m, 0m, "Cashbox Receipt"),
            new(SystemAccountKeys.AccountsReceivable, 0m, 1000m, customer.Name)
        }, $"SEED-TR1-{voucher.Id}");
        voucher.JournalEntryId = vJe.Id;
        db.TreasuryVouchers.Add(voucher);

        // 8. System Notifications
        db.Notifications.AddRange(
            new Notification
            {
                TenantId = tenantId,
                Type = NotificationType.LowStock,
                Title = "Low Stock: Food-Grade Yeast (RM-104)",
                TitleAr = "تنبيه نقص مخزون: خميرة غذائية فورية (RM-104)",
                Message = "Available quantity (50 KG) is below minimum threshold (80 KG).",
                MessageAr = "الكمية المتاحة (50 كجم) أقل من الحد الأدنى (80 كجم).",
                ReferenceEntity = nameof(RawMaterial),
                ReferenceId = rm4.Id
            },
            new Notification
            {
                TenantId = tenantId,
                Type = NotificationType.MachineMaintenance,
                Title = "Upcoming Maintenance: High-Speed Mixer 500L",
                TitleAr = "موعد صيانة قريب: خلاط صناعي سريع 500 لتر",
                Message = "Scheduled preventive maintenance due within 15 days.",
                MessageAr = "الصيانة الوقائية الدورية مستحقة خلال 15 يوماً.",
                ReferenceEntity = nameof(Machine),
                ReferenceId = machine.Id
            }
        );

        await db.SaveChangesAsync();
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

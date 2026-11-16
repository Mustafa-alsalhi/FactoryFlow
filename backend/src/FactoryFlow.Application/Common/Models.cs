namespace FactoryFlow.Application.Common;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize));
}

public class PagedQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}

public static class DefaultRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string CompanyOwner = "Company Owner";
    public const string GeneralManager = "General Manager";
    public const string Accountant = "Accountant";
    public const string ProductionManager = "Production Manager";
    public const string WarehouseManager = "Warehouse Manager";
    public const string PurchasingManager = "Purchasing Manager";
    public const string SalesManager = "Sales Manager";
    public const string QualityController = "Quality Controller";
    public const string Employee = "Employee";

    public static readonly string[] All =
    {
        SuperAdmin, CompanyOwner, GeneralManager, Accountant,
        ProductionManager, WarehouseManager, PurchasingManager,
        SalesManager, QualityController, Employee
    };
}

public static class SystemAccountKeys
{
    public const string Cash = "CASH";
    public const string Bank = "BANK";
    public const string AccountsReceivable = "AR";
    public const string AccountsPayable = "AP";
    public const string RawMaterialInventory = "RAW_INV";
    public const string WorkInProgress = "WIP";
    public const string FinishedGoodsInventory = "FG_INV";
    public const string CostOfGoodsSold = "COGS";
    public const string SalesRevenue = "SALES";
    public const string DirectLaborExpense = "WAGES";
    public const string ManufacturingOverhead = "OVERHEAD";
    public const string TaxPayable = "TAX_PAYABLE";
    public const string TaxReceivable = "TAX_RECEIVABLE";
    public const string ScrapLoss = "SCRAP_LOSS";
    public const string InventoryAdjustment = "INV_ADJ";
    public const string RetainedEarnings = "RETAINED_EARNINGS";
}


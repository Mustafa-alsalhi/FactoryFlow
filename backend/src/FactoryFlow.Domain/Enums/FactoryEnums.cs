namespace FactoryFlow.Domain.Enums;

public enum InventoryValuationMethod
{
    FIFO = 1,
    WeightedAverage = 2
}

public enum OverheadAllocationMethod
{
    LaborHours = 1,
    MachineHours = 2
}

public enum ProductionOrderStatus
{
    Draft = 1,
    Planned = 2,
    InProgress = 3,
    Paused = 4,
    Completed = 5,
    Cancelled = 6
}

public enum ProductType
{
    SemiFinished = 1,
    Finished = 2
}

public enum WageType
{
    Hourly = 1,
    Daily = 2,
    Monthly = 3
}

public enum StockTransactionType
{
    PurchaseReceipt = 1,
    ProductionIssue = 2,
    ProductionReturn = 3,
    ProductionOutput = 4,
    TransferOut = 5,
    TransferIn = 6,
    AdjustmentIn = 7,
    AdjustmentOut = 8,
    SalesIssue = 9,
    SalesReturn = 10,
    ScrapWriteOff = 11
}

public enum PurchaseOrderStatus
{
    Draft = 1,
    Approved = 2,
    PartiallyReceived = 3,
    Received = 4,
    Cancelled = 5
}

public enum SalesOrderStatus
{
    Draft = 1,
    Confirmed = 2,
    PartiallyDelivered = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum InvoiceStatus
{
    Draft = 1,
    Posted = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5
}

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

public enum QualityInspectionType
{
    RawMaterial = 1,
    InProcess = 2,
    FinishedProduct = 3
}

public enum QualityInspectionStatus
{
    Pending = 1,
    Passed = 2,
    Partial = 3,
    Failed = 4
}

public enum VoucherType
{
    Receipt = 1,
    Payment = 2,
    Transfer = 3
}

public enum NotificationType
{
    LowStock = 1,
    ExpiringMaterial = 2,
    DelayedProduction = 3,
    CostOverrun = 4,
    DefectiveProduct = 5,
    OverdueCustomer = 6,
    DueSupplier = 7,
    MachineMaintenance = 8
}


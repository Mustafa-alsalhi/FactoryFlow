using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Enums;

namespace FactoryFlow.Domain.Entities;

public class Currency : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = "$";
    public bool IsBase { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ExchangeRate : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string FromCurrencyCode { get; set; } = string.Empty;
    public string ToCurrencyCode { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
}

public class TaxRate : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public decimal RatePercentage { get; set; }
    public bool IsExempt { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Account : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public AccountType AccountType { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsSystemAccount { get; set; }
    public string? SystemKey { get; set; } // e.g., CASH, BANK, AR, AP, RAW_INV, WIP, FG_INV, COGS, SALES, WAGES, OVERHEAD, TAX_PAYABLE, TAX_RECEIVABLE
    public bool IsActive { get; set; } = true;

    public Account? ParentAccount { get; set; }
}

public class JournalEntry : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;
    public Guid? ReversedByEntryId { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();

    public bool IsBalanced() => Math.Round(Lines.Sum(l => l.Debit), 4) == Math.Round(Lines.Sum(l => l.Credit), 4);
}

public class JournalEntryLine : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal ForeignAmount { get; set; }
    public string? Memo { get; set; }

    public JournalEntry? JournalEntry { get; set; }
    public Account? Account { get; set; }
}

public class Cashbox : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal CurrentBalance { get; set; }
    public Guid AccountId { get; set; }
    public DateTime? LastClosedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public Branch? Branch { get; set; }
    public Account? Account { get; set; }
}

public class BankAccount : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    public decimal CurrentBalance { get; set; }
    public Guid AccountId { get; set; }
    public bool IsActive { get; set; } = true;

    public Account? Account { get; set; }
}

public class TreasuryVoucher : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public VoucherType VoucherType { get; set; }
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;
    public Guid? CashboxId { get; set; }
    public Guid? BankAccountId { get; set; }
    public Guid? TargetCashboxId { get; set; }
    public Guid? TargetBankAccountId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? CounterAccountId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1m;
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? IdempotencyKey { get; set; }

    public Cashbox? Cashbox { get; set; }
    public BankAccount? BankAccount { get; set; }
    public Customer? Customer { get; set; }
    public Supplier? Supplier { get; set; }
}


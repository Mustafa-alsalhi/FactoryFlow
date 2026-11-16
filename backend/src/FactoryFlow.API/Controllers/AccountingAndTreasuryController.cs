using FactoryFlow.Application.Common;
using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AccountingController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountingService _accounting;
    private readonly IAuditService _audit;

    public AccountingController(IApplicationDbContext db, ICurrentUserService currentUser, IAccountingService accounting, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _accounting = accounting;
        _audit = audit;
    }

    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(CancellationToken ct) =>
        Ok(await _db.Accounts.OrderBy(a => a.Code).ToListAsync(ct));

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] Account acc, CancellationToken ct)
    {
        acc.Id = Guid.NewGuid();
        acc.TenantId = TenantId;
        _db.Accounts.Add(acc);
        await _db.SaveChangesAsync(ct);
        return Ok(acc);
    }

    [HttpGet("journal-entries")]
    public async Task<IActionResult> GetJournalEntries(CancellationToken ct) =>
        Ok(await _db.JournalEntries
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .OrderByDescending(j => j.EntryDate)
            .Take(200)
            .ToListAsync(ct));

    public record ManualJournalRequest(DateTime EntryDate, string Description, List<JournalLineInput> Lines);

    [HttpPost("journal-entries")]
    public async Task<IActionResult> CreateJournalEntry([FromBody] ManualJournalRequest req, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(ct);
        var je = await _accounting.CreateAndPostAsync(TenantId, req.EntryDate, req.Description, "Manual", null, req.Lines, null, ct);
        await tx.CommitAsync(ct);
        await _audit.LogAsync(TenantId, "Post", nameof(JournalEntry), je.Id.ToString(), newValues: new { je.EntryNumber, je.Description }, ct: ct);
        return Ok(je);
    }

    public record ReverseRequest(string Reason);

    [HttpPost("journal-entries/{id:guid}/reverse")]
    public async Task<IActionResult> ReverseJournalEntry(Guid id, [FromBody] ReverseRequest req, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(ct);
        var rev = await _accounting.ReverseEntryAsync(TenantId, id, req.Reason, ct);
        await tx.CommitAsync(ct);
        await _audit.LogAsync(TenantId, "Reverse", nameof(JournalEntry), id.ToString(), newValues: new { ReversedBy = rev.EntryNumber, req.Reason }, ct: ct);
        return Ok(rev);
    }

    [HttpGet("general-ledger/{accountId:guid}")]
    public async Task<IActionResult> GetGeneralLedger(Guid accountId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var q = _db.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == accountId && l.JournalEntry!.Status == JournalEntryStatus.Posted);

        if (from.HasValue) q = q.Where(l => l.JournalEntry!.EntryDate >= from.Value);
        if (to.HasValue) q = q.Where(l => l.JournalEntry!.EntryDate <= to.Value);

        var lines = await q.OrderBy(l => l.JournalEntry!.EntryDate).Select(l => new
        {
            l.JournalEntry!.EntryNumber,
            l.JournalEntry.EntryDate,
            l.JournalEntry.Description,
            l.Debit,
            l.Credit,
            l.Memo
        }).ToListAsync(ct);

        return Ok(lines);
    }

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var q = _db.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted);

        if (from.HasValue) q = q.Where(l => l.JournalEntry!.EntryDate >= from.Value);
        if (to.HasValue) q = q.Where(l => l.JournalEntry!.EntryDate <= to.Value);

        var rows = await q.GroupBy(l => new { l.AccountId, l.Account!.Code, l.Account.Name, l.Account.NameAr, l.Account.AccountType })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Code,
                g.Key.Name,
                g.Key.NameAr,
                g.Key.AccountType,
                TotalDebit = g.Sum(x => x.Debit),
                TotalCredit = g.Sum(x => x.Credit),
                NetBalance = g.Sum(x => x.Debit - x.Credit)
            })
            .OrderBy(r => r.Code)
            .ToListAsync(ct);

        return Ok(new
        {
            rows,
            totalDebit = rows.Sum(r => r.TotalDebit),
            totalCredit = rows.Sum(r => r.TotalCredit)
        });
    }

    [HttpGet("income-statement")]
    public async Task<IActionResult> GetIncomeStatement([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var start = from ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
        var end = to ?? DateTime.UtcNow.AddDays(1);

        var lines = await _db.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                && l.JournalEntry.EntryDate >= start && l.JournalEntry.EntryDate <= end
                && (l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
            .ToListAsync(ct);

        var revenues = lines.Where(l => l.Account!.AccountType == AccountType.Revenue)
            .GroupBy(l => new { l.Account!.Code, l.Account.Name, l.Account.NameAr })
            .Select(g => new { g.Key.Code, g.Key.Name, g.Key.NameAr, Amount = g.Sum(x => x.Credit - x.Debit) }).ToList();

        var expenses = lines.Where(l => l.Account!.AccountType == AccountType.Expense)
            .GroupBy(l => new { l.Account!.Code, l.Account.Name, l.Account.NameAr })
            .Select(g => new { g.Key.Code, g.Key.Name, g.Key.NameAr, Amount = g.Sum(x => x.Debit - x.Credit) }).ToList();

        var totalRev = revenues.Sum(r => r.Amount);
        var totalExp = expenses.Sum(e => e.Amount);

        return Ok(new { revenues, expenses, totalRevenue = totalRev, totalExpenses = totalExp, netIncome = totalRev - totalExp });
    }

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> GetBalanceSheet([FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var date = asOf ?? DateTime.UtcNow.AddDays(1);
        var lines = await _db.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted && l.JournalEntry.EntryDate <= date)
            .ToListAsync(ct);

        var assets = lines.Where(l => l.Account!.AccountType == AccountType.Asset)
            .GroupBy(l => new { l.Account!.Code, l.Account.Name, l.Account.NameAr })
            .Select(g => new { g.Key.Code, g.Key.Name, g.Key.NameAr, Balance = g.Sum(x => x.Debit - x.Credit) }).ToList();

        var liabilities = lines.Where(l => l.Account!.AccountType == AccountType.Liability)
            .GroupBy(l => new { l.Account!.Code, l.Account.Name, l.Account.NameAr })
            .Select(g => new { g.Key.Code, g.Key.Name, g.Key.NameAr, Balance = g.Sum(x => x.Credit - x.Debit) }).ToList();

        var equity = lines.Where(l => l.Account!.AccountType == AccountType.Equity)
            .GroupBy(l => new { l.Account!.Code, l.Account.Name, l.Account.NameAr })
            .Select(g => new { g.Key.Code, g.Key.Name, g.Key.NameAr, Balance = g.Sum(x => x.Credit - x.Debit) }).ToList();

        var netIncome = lines.Where(l => l.Account!.AccountType == AccountType.Revenue).Sum(l => l.Credit - l.Debit)
            - lines.Where(l => l.Account!.AccountType == AccountType.Expense).Sum(l => l.Debit - l.Credit);

        return Ok(new
        {
            assets,
            liabilities,
            equity,
            currentPeriodNetIncome = netIncome,
            totalAssets = assets.Sum(a => a.Balance),
            totalLiabilitiesAndEquity = liabilities.Sum(l => l.Balance) + equity.Sum(e => e.Balance) + netIncome
        });
    }

    // --- Currencies & Taxes ---
    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies(CancellationToken ct) =>
        Ok(await _db.Currencies.OrderByDescending(c => c.IsBase).ThenBy(c => c.Code).ToListAsync(ct));

    [HttpPost("currencies")]
    public async Task<IActionResult> CreateCurrency([FromBody] Currency c, CancellationToken ct)
    {
        c.Id = Guid.NewGuid();
        c.TenantId = TenantId;
        _db.Currencies.Add(c);
        await _db.SaveChangesAsync(ct);
        return Ok(c);
    }

    [HttpGet("exchange-rates")]
    public async Task<IActionResult> GetExchangeRates(CancellationToken ct) =>
        Ok(await _db.ExchangeRates.OrderByDescending(e => e.EffectiveDate).ToListAsync(ct));

    [HttpPost("exchange-rates")]
    public async Task<IActionResult> CreateExchangeRate([FromBody] ExchangeRate r, CancellationToken ct)
    {
        r.Id = Guid.NewGuid();
        r.TenantId = TenantId;
        _db.ExchangeRates.Add(r);
        await _db.SaveChangesAsync(ct);
        return Ok(r);
    }

    [HttpGet("tax-rates")]
    public async Task<IActionResult> GetTaxRates(CancellationToken ct) =>
        Ok(await _db.TaxRates.OrderBy(t => t.Code).ToListAsync(ct));

    [HttpPost("tax-rates")]
    public async Task<IActionResult> CreateTaxRate([FromBody] TaxRate t, CancellationToken ct)
    {
        t.Id = Guid.NewGuid();
        t.TenantId = TenantId;
        _db.TaxRates.Add(t);
        await _db.SaveChangesAsync(ct);
        return Ok(t);
    }

    // --- Treasury (Cashboxes & Banks) ---
    [HttpGet("cashboxes")]
    public async Task<IActionResult> GetCashboxes(CancellationToken ct) =>
        Ok(await _db.Cashboxes.Include(c => c.Branch).ToListAsync(ct));

    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetBankAccounts(CancellationToken ct) =>
        Ok(await _db.BankAccounts.ToListAsync(ct));

    [HttpGet("vouchers")]
    public async Task<IActionResult> GetVouchers(CancellationToken ct) =>
        Ok(await _db.TreasuryVouchers
            .Include(v => v.Cashbox).Include(v => v.BankAccount)
            .Include(v => v.Customer).Include(v => v.Supplier)
            .OrderByDescending(v => v.VoucherDate).Take(200).ToListAsync(ct));

    [HttpPost("vouchers")]
    public async Task<IActionResult> CreateVoucher([FromBody] TreasuryVoucher v, CancellationToken ct)
    {
        if (v.Amount <= 0) throw new DomainException("Voucher amount must be greater than zero.");

        await using var tx = await _db.BeginTransactionAsync(ct);
        var count = await _db.TreasuryVouchers.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, ct);
        v.Id = Guid.NewGuid();
        v.TenantId = TenantId;
        v.VoucherNumber = $"TR-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";

        var baseAmount = Math.Round(v.Amount * (v.ExchangeRate <= 0 ? 1m : v.ExchangeRate), 4);
        string treasuryAccKey = v.CashboxId.HasValue ? SystemAccountKeys.Cash : SystemAccountKeys.Bank;

        if (v.CashboxId.HasValue)
        {
            var cb = await _db.Cashboxes.FirstOrDefaultAsync(c => c.Id == v.CashboxId.Value, ct) ?? throw new KeyNotFoundException("Cashbox not found.");
            cb.CurrentBalance += v.VoucherType == VoucherType.Receipt ? v.Amount : -v.Amount;
        }
        else if (v.BankAccountId.HasValue)
        {
            var bk = await _db.BankAccounts.FirstOrDefaultAsync(b => b.Id == v.BankAccountId.Value, ct) ?? throw new KeyNotFoundException("Bank account not found.");
            bk.CurrentBalance += v.VoucherType == VoucherType.Receipt ? v.Amount : -v.Amount;
        }

        var lines = new List<JournalLineInput>();
        if (v.VoucherType == VoucherType.Receipt)
        {
            string creditKey = v.CustomerId.HasValue ? SystemAccountKeys.AccountsReceivable : (v.CounterAccountId?.ToString() ?? SystemAccountKeys.SalesRevenue);
            if (v.CustomerId.HasValue)
            {
                var cust = await _db.Customers.FirstOrDefaultAsync(c => c.Id == v.CustomerId.Value, ct);
                if (cust != null) cust.Balance -= baseAmount;
            }
            lines.Add(new(treasuryAccKey, baseAmount, 0m, v.Description ?? v.VoucherNumber));
            lines.Add(new(creditKey, 0m, baseAmount, v.Description ?? v.VoucherNumber));
        }
        else if (v.VoucherType == VoucherType.Payment)
        {
            string debitKey = v.SupplierId.HasValue ? SystemAccountKeys.AccountsPayable : (v.CounterAccountId?.ToString() ?? SystemAccountKeys.ManufacturingOverhead);
            if (v.SupplierId.HasValue)
            {
                var sup = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == v.SupplierId.Value, ct);
                if (sup != null) sup.Balance -= baseAmount;
            }
            lines.Add(new(debitKey, baseAmount, 0m, v.Description ?? v.VoucherNumber));
            lines.Add(new(treasuryAccKey, 0m, baseAmount, v.Description ?? v.VoucherNumber));
        }
        else
        {
            // Transfer
            if (v.TargetCashboxId.HasValue)
            {
                var tcb = await _db.Cashboxes.FirstOrDefaultAsync(c => c.Id == v.TargetCashboxId.Value, ct);
                if (tcb != null) tcb.CurrentBalance += v.Amount;
            }
            if (v.TargetBankAccountId.HasValue)
            {
                var tbk = await _db.BankAccounts.FirstOrDefaultAsync(b => b.Id == v.TargetBankAccountId.Value, ct);
                if (tbk != null) tbk.CurrentBalance += v.Amount;
            }
            string targetKey = v.TargetCashboxId.HasValue ? SystemAccountKeys.Cash : SystemAccountKeys.Bank;
            lines.Add(new(targetKey, baseAmount, 0m, $"Transfer In {v.VoucherNumber}"));
            lines.Add(new(treasuryAccKey, 0m, baseAmount, $"Transfer Out {v.VoucherNumber}"));
        }

        var je = await _accounting.CreateAndPostAsync(TenantId, v.VoucherDate, v.Description ?? $"Voucher {v.VoucherNumber}", nameof(TreasuryVoucher), v.Id, lines, $"TR-{v.Id}", ct);
        v.JournalEntryId = je.Id;

        _db.TreasuryVouchers.Add(v);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(v);
    }

    [HttpPost("cashboxes/{id:guid}/close")]
    public async Task<IActionResult> CloseCashbox(Guid id, CancellationToken ct)
    {
        var cb = await _db.Cashboxes.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException("Cashbox not found.");
        cb.LastClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(TenantId, "DailyClose", nameof(Cashbox), cb.Id.ToString(), newValues: new { cb.CurrentBalance, cb.LastClosedAt }, ct: ct);
        return Ok(cb);
    }
}


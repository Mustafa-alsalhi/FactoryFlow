using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.Infrastructure.Services;

public class AccountingService : IAccountingService
{
    private readonly IApplicationDbContext _db;

    public AccountingService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<JournalEntry> CreateAndPostAsync(
        Guid tenantId,
        DateTime entryDate,
        string description,
        string? referenceType,
        Guid? referenceId,
        List<JournalLineInput> lines,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (lines == null || lines.Count < 2)
            throw new DomainException("A journal entry must have at least two lines.");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.JournalEntries
                .Include(j => j.Lines)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.IdempotencyKey == idempotencyKey, ct);
            if (existing != null)
                return existing;
        }

        var totalDebit = Math.Round(lines.Sum(l => l.Debit), 4);
        var totalCredit = Math.Round(lines.Sum(l => l.Credit), 4);

        if (totalDebit <= 0 || totalDebit != totalCredit)
            throw new DomainException($"Journal entry is unbalanced. Debit: {totalDebit}, Credit: {totalCredit}");

        var accounts = await _db.Accounts
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .ToListAsync(ct);

        var count = await _db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => j.TenantId == tenantId, ct);

        var entry = new JournalEntry
        {
            TenantId = tenantId,
            EntryNumber = $"JE-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}",
            EntryDate = entryDate,
            Description = description,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Status = JournalEntryStatus.Posted,
            IdempotencyKey = idempotencyKey
        };

        foreach (var line in lines)
        {
            if (line.Debit < 0 || line.Credit < 0 || (line.Debit > 0 && line.Credit > 0))
                throw new DomainException("Each journal line must have either a non-negative debit or credit, not both.");

            Account? account = Guid.TryParse(line.AccountKeyOrId, out var accId)
                ? accounts.FirstOrDefault(a => a.Id == accId)
                : accounts.FirstOrDefault(a => a.SystemKey == line.AccountKeyOrId || a.Code == line.AccountKeyOrId);

            if (account == null)
                throw new DomainException($"Account '{line.AccountKeyOrId}' not found for tenant.");

            entry.Lines.Add(new JournalEntryLine
            {
                TenantId = tenantId,
                JournalEntryId = entry.Id,
                AccountId = account.Id,
                Debit = Math.Round(line.Debit, 4),
                Credit = Math.Round(line.Credit, 4),
                CurrencyCode = line.CurrencyCode,
                ExchangeRate = line.ExchangeRate <= 0 ? 1m : line.ExchangeRate,
                ForeignAmount = line.ForeignAmount,
                Memo = line.Memo
            });
        }

        if (!entry.IsBalanced())
            throw new DomainException("Journal entry failed final balance verification.");

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<JournalEntry> ReverseEntryAsync(Guid tenantId, Guid journalEntryId, string reason, CancellationToken ct = default)
    {
        var original = await _db.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == journalEntryId, ct);

        if (original == null)
            throw new DomainException("Journal entry not found.");

        if (original.Status == JournalEntryStatus.Reversed)
            throw new DomainException("Journal entry is already reversed.");

        if (original.Status != JournalEntryStatus.Posted)
            throw new DomainException("Only posted journal entries can be reversed.");

        var reversalLines = original.Lines.Select(l => new JournalLineInput(
            l.AccountId.ToString(),
            l.Credit,
            l.Debit,
            $"Reversal: {reason} ({l.Memo})",
            l.CurrencyCode,
            l.ExchangeRate,
            l.ForeignAmount
        )).ToList();

        var reversal = await CreateAndPostAsync(
            tenantId,
            DateTime.UtcNow,
            $"Reversal of {original.EntryNumber}: {reason}",
            "JournalReversal",
            original.Id,
            reversalLines,
            $"REV-{original.Id}",
            ct);

        original.Status = JournalEntryStatus.Reversed;
        original.ReversedByEntryId = reversal.Id;
        await _db.SaveChangesAsync(ct);

        return reversal;
    }
}


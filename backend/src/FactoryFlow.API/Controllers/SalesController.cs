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
public class SalesController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IInventoryService _inventory;
    private readonly IAccountingService _accounting;
    private readonly IAuditService _audit;

    public SalesController(IApplicationDbContext db, ICurrentUserService currentUser, IInventoryService inventory, IAccountingService accounting, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _inventory = inventory;
        _accounting = accounting;
        _audit = audit;
    }

    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(CancellationToken ct) =>
        Ok(await _db.Customers.OrderBy(c => c.Code).ToListAsync(ct));

    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer([FromBody] Customer cust, CancellationToken ct)
    {
        cust.Id = Guid.NewGuid();
        cust.TenantId = TenantId;
        _db.Customers.Add(cust);
        await _db.SaveChangesAsync(ct);
        return Ok(cust);
    }

    [HttpGet("customers/{id:guid}/statement")]
    public async Task<IActionResult> GetCustomerStatement(Guid id, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException("Customer not found.");
        var invoices = await _db.SalesInvoices.Where(i => i.CustomerId == id && i.Status != InvoiceStatus.Cancelled).OrderBy(i => i.InvoiceDate).ToListAsync(ct);
        var payments = await _db.TreasuryVouchers.Where(v => v.CustomerId == id).OrderBy(v => v.VoucherDate).ToListAsync(ct);
        return Ok(new { customer, invoices, payments });
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetSalesOrders(CancellationToken ct) =>
        Ok(await _db.SalesOrders.Include(o => o.Customer).Include(o => o.Warehouse).Include(o => o.Items).ThenInclude(i => i.Product)
            .OrderByDescending(o => o.OrderDate).ToListAsync(ct));

    [HttpPost("orders")]
    public async Task<IActionResult> CreateSalesOrder([FromBody] SalesOrder so, CancellationToken ct)
    {
        var count = await _db.SalesOrders.IgnoreQueryFilters().CountAsync(o => o.TenantId == TenantId, ct);
        so.Id = Guid.NewGuid();
        so.TenantId = TenantId;
        so.OrderNumber = $"SO-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
        foreach (var item in so.Items)
        {
            item.Id = Guid.NewGuid();
            item.TenantId = TenantId;
            item.SalesOrderId = so.Id;
            item.LineTotal = Math.Round(item.Quantity * item.UnitPrice * (1m + item.TaxRate / 100m), 4);
        }
        so.SubTotal = so.Items.Sum(i => i.Quantity * i.UnitPrice);
        so.TaxAmount = so.Items.Sum(i => i.LineTotal - (i.Quantity * i.UnitPrice));
        so.TotalAmount = so.SubTotal + so.TaxAmount - so.DiscountAmount;
        _db.SalesOrders.Add(so);
        await _db.SaveChangesAsync(ct);
        return Ok(so);
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(CancellationToken ct) =>
        Ok(await _db.SalesInvoices.Include(i => i.Customer).Include(i => i.Warehouse).Include(i => i.Items).ThenInclude(it => it.Product)
            .OrderByDescending(i => i.InvoiceDate).ToListAsync(ct));

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateAndPostInvoice([FromBody] SalesInvoice inv, CancellationToken ct)
    {
        if (inv.Items == null || inv.Items.Count == 0)
            throw new DomainException("Sales invoice must contain at least one item.");

        await using var tx = await _db.BeginTransactionAsync(ct);

        var count = await _db.SalesInvoices.IgnoreQueryFilters().CountAsync(i => i.TenantId == TenantId, ct);
        inv.Id = Guid.NewGuid();
        inv.TenantId = TenantId;
        inv.InvoiceNumber = $"{(inv.IsReturn ? "SR" : "SI")}-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
        inv.Status = inv.IsCashSale ? InvoiceStatus.Paid : InvoiceStatus.Posted;

        decimal subTotal = 0m;
        decimal taxTotal = 0m;
        decimal totalCogs = 0m;

        foreach (var item in inv.Items)
        {
            item.Id = Guid.NewGuid();
            item.TenantId = TenantId;
            item.SalesInvoiceId = inv.Id;
            var lineNet = Math.Round(item.Quantity * item.UnitPrice, 4);
            item.LineTotal = lineNet + item.TaxAmount;
            subTotal += lineNet;
            taxTotal += item.TaxAmount;

            if (!inv.IsReturn)
            {
                var invTx = await _inventory.IssueAsync(TenantId, inv.WarehouseId, null, item.ProductId, item.Quantity,
                    StockTransactionType.SalesIssue, nameof(SalesInvoice), inv.Id, $"Sales {inv.InvoiceNumber}", null, ct);
                item.UnitCost = invTx.UnitCost;
                totalCogs += invTx.TotalCost;
            }
            else
            {
                var prod = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
                var unitCost = prod?.StandardCost ?? 0m;
                var invTx = await _inventory.ReceiveAsync(TenantId, inv.WarehouseId, null, item.ProductId, item.Quantity, unitCost,
                    StockTransactionType.SalesReturn, null, null, nameof(SalesInvoice), inv.Id, $"Sales Return {inv.InvoiceNumber}", null, ct);
                item.UnitCost = unitCost;
                totalCogs += invTx.TotalCost;
            }
        }

        inv.SubTotal = subTotal;
        inv.TaxAmount = taxTotal;
        inv.TotalAmount = subTotal + taxTotal - inv.DiscountAmount;
        inv.TotalCogs = totalCogs;
        if (inv.IsCashSale) inv.PaidAmount = inv.TotalAmount;

        var baseNetSales = Math.Round((subTotal - inv.DiscountAmount) * inv.ExchangeRate, 4);
        var baseTax = Math.Round(taxTotal * inv.ExchangeRate, 4);
        var baseTotal = Math.Round(inv.TotalAmount * inv.ExchangeRate, 4);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == inv.CustomerId, ct)
            ?? throw new KeyNotFoundException("Customer not found.");

        var debitAccountKey = inv.IsCashSale ? SystemAccountKeys.Cash : SystemAccountKeys.AccountsReceivable;

        if (!inv.IsReturn)
        {
            if (!inv.IsCashSale) customer.Balance += baseTotal;

            var lines = new List<JournalLineInput>
            {
                new(debitAccountKey, baseTotal, 0m, $"Sales {inv.InvoiceNumber}"),
                new(SystemAccountKeys.SalesRevenue, 0m, baseNetSales, $"Revenue {inv.InvoiceNumber}")
            };
            if (baseTax > 0)
                lines.Add(new(SystemAccountKeys.TaxPayable, 0m, baseTax, $"Output VAT {inv.InvoiceNumber}"));

            if (totalCogs > 0)
            {
                lines.Add(new(SystemAccountKeys.CostOfGoodsSold, totalCogs, 0m, $"COGS {inv.InvoiceNumber}"));
                lines.Add(new(SystemAccountKeys.FinishedGoodsInventory, 0m, totalCogs, $"FG Issue {inv.InvoiceNumber}"));
            }

            var je = await _accounting.CreateAndPostAsync(TenantId, inv.InvoiceDate, $"Sales Invoice {inv.InvoiceNumber}", nameof(SalesInvoice), inv.Id, lines, $"SI-{inv.Id}", ct);
            inv.JournalEntryId = je.Id;
        }
        else
        {
            if (!inv.IsCashSale) customer.Balance -= baseTotal;

            var lines = new List<JournalLineInput>
            {
                new(SystemAccountKeys.SalesRevenue, baseNetSales, 0m, $"Sales Return {inv.InvoiceNumber}"),
                new(debitAccountKey, 0m, baseTotal, $"Credit Customer {inv.InvoiceNumber}")
            };
            if (baseTax > 0)
                lines.Add(new(SystemAccountKeys.TaxPayable, baseTax, 0m, $"Output VAT Return {inv.InvoiceNumber}"));

            if (totalCogs > 0)
            {
                lines.Add(new(SystemAccountKeys.FinishedGoodsInventory, totalCogs, 0m, $"FG Return {inv.InvoiceNumber}"));
                lines.Add(new(SystemAccountKeys.CostOfGoodsSold, 0m, totalCogs, $"COGS Reversal {inv.InvoiceNumber}"));
            }

            var je = await _accounting.CreateAndPostAsync(TenantId, inv.InvoiceDate, $"Sales Return {inv.InvoiceNumber}", nameof(SalesInvoice), inv.Id, lines, $"SR-{inv.Id}", ct);
            inv.JournalEntryId = je.Id;
        }

        _db.SalesInvoices.Add(inv);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await _audit.LogAsync(TenantId, "Post", nameof(SalesInvoice), inv.Id.ToString(), newValues: new { inv.InvoiceNumber, inv.TotalAmount }, ct: ct);

        return Ok(inv);
    }
}


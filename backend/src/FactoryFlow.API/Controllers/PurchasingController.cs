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
public class PurchasingController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IInventoryService _inventory;
    private readonly IAccountingService _accounting;
    private readonly IAuditService _audit;

    public PurchasingController(IApplicationDbContext db, ICurrentUserService currentUser, IInventoryService inventory, IAccountingService accounting, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _inventory = inventory;
        _accounting = accounting;
        _audit = audit;
    }

    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(CancellationToken ct) =>
        Ok(await _db.Suppliers.OrderBy(s => s.Code).ToListAsync(ct));

    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier([FromBody] Supplier sup, CancellationToken ct)
    {
        sup.Id = Guid.NewGuid();
        sup.TenantId = TenantId;
        _db.Suppliers.Add(sup);
        await _db.SaveChangesAsync(ct);
        return Ok(sup);
    }

    [HttpGet("suppliers/{id:guid}/statement")]
    public async Task<IActionResult> GetSupplierStatement(Guid id, CancellationToken ct)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new KeyNotFoundException("Supplier not found.");
        var invoices = await _db.PurchaseInvoices.Where(i => i.SupplierId == id && i.Status != InvoiceStatus.Cancelled).OrderBy(i => i.InvoiceDate).ToListAsync(ct);
        var payments = await _db.TreasuryVouchers.Where(v => v.SupplierId == id).OrderBy(v => v.VoucherDate).ToListAsync(ct);
        return Ok(new { supplier, invoices, payments });
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetPurchaseOrders(CancellationToken ct) =>
        Ok(await _db.PurchaseOrders.Include(o => o.Supplier).Include(o => o.Warehouse).Include(o => o.Items).ThenInclude(i => i.RawMaterial)
            .OrderByDescending(o => o.OrderDate).ToListAsync(ct));

    [HttpPost("orders")]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] PurchaseOrder po, CancellationToken ct)
    {
        var count = await _db.PurchaseOrders.IgnoreQueryFilters().CountAsync(o => o.TenantId == TenantId, ct);
        po.Id = Guid.NewGuid();
        po.TenantId = TenantId;
        po.OrderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
        foreach (var item in po.Items)
        {
            item.Id = Guid.NewGuid();
            item.TenantId = TenantId;
            item.PurchaseOrderId = po.Id;
            item.LineTotal = Math.Round(item.Quantity * item.UnitPrice * (1m + item.TaxRate / 100m), 4);
        }
        po.SubTotal = po.Items.Sum(i => i.Quantity * i.UnitPrice);
        po.TaxAmount = po.Items.Sum(i => i.LineTotal - (i.Quantity * i.UnitPrice));
        po.TotalAmount = po.SubTotal + po.TaxAmount - po.DiscountAmount;
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync(ct);
        return Ok(po);
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(CancellationToken ct) =>
        Ok(await _db.PurchaseInvoices.Include(i => i.Supplier).Include(i => i.Warehouse).Include(i => i.Items).ThenInclude(it => it.RawMaterial)
            .OrderByDescending(i => i.InvoiceDate).ToListAsync(ct));

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateAndPostInvoice([FromBody] PurchaseInvoice inv, CancellationToken ct)
    {
        if (inv.Items == null || inv.Items.Count == 0)
            throw new DomainException("Purchase invoice must contain at least one item.");

        await using var tx = await _db.BeginTransactionAsync(ct);

        var count = await _db.PurchaseInvoices.IgnoreQueryFilters().CountAsync(i => i.TenantId == TenantId, ct);
        inv.Id = Guid.NewGuid();
        inv.TenantId = TenantId;
        inv.InvoiceNumber = $"{(inv.IsReturn ? "PR" : "PI")}-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
        inv.Status = InvoiceStatus.Posted;

        decimal subTotal = 0m;
        decimal taxTotal = 0m;

        foreach (var item in inv.Items)
        {
            item.Id = Guid.NewGuid();
            item.TenantId = TenantId;
            item.PurchaseInvoiceId = inv.Id;
            var lineNet = Math.Round(item.Quantity * item.UnitPrice, 4);
            item.LineTotal = lineNet + item.TaxAmount;
            subTotal += lineNet;
            taxTotal += item.TaxAmount;

            var rm = await _db.RawMaterials.FirstOrDefaultAsync(r => r.Id == item.RawMaterialId, ct);
            if (rm != null && !inv.IsReturn)
                rm.LastPurchasePrice = item.UnitPrice;

            if (!inv.IsReturn)
            {
                await _inventory.ReceiveAsync(TenantId, inv.WarehouseId, item.RawMaterialId, null, item.Quantity,
                    item.UnitPrice * inv.ExchangeRate, StockTransactionType.PurchaseReceipt, item.BatchNumber, item.ExpiryDate,
                    nameof(PurchaseInvoice), inv.Id, $"Purchase {inv.InvoiceNumber}", null, ct);
            }
            else
            {
                await _inventory.IssueAsync(TenantId, inv.WarehouseId, item.RawMaterialId, null, item.Quantity,
                    StockTransactionType.AdjustmentOut, nameof(PurchaseInvoice), inv.Id, $"Purchase Return {inv.InvoiceNumber}", null, ct);
            }
        }

        inv.SubTotal = subTotal;
        inv.TaxAmount = taxTotal;
        inv.TotalAmount = subTotal + taxTotal - inv.DiscountAmount;

        var baseSubTotal = Math.Round((subTotal - inv.DiscountAmount) * inv.ExchangeRate, 4);
        var baseTax = Math.Round(taxTotal * inv.ExchangeRate, 4);
        var baseTotal = Math.Round(inv.TotalAmount * inv.ExchangeRate, 4);

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == inv.SupplierId, ct)
            ?? throw new KeyNotFoundException("Supplier not found.");

        if (!inv.IsReturn)
        {
            supplier.Balance += baseTotal;
            var lines = new List<JournalLineInput>
            {
                new(SystemAccountKeys.RawMaterialInventory, baseSubTotal, 0m, $"Purchase {inv.InvoiceNumber}"),
                new(SystemAccountKeys.AccountsPayable, 0m, baseTotal, $"AP {supplier.Name}")
            };
            if (baseTax > 0)
                lines.Add(new(SystemAccountKeys.TaxReceivable, baseTax, 0m, $"Input VAT {inv.InvoiceNumber}"));

            var je = await _accounting.CreateAndPostAsync(TenantId, inv.InvoiceDate, $"Purchase Invoice {inv.InvoiceNumber}", nameof(PurchaseInvoice), inv.Id, lines, $"PI-{inv.Id}", ct);
            inv.JournalEntryId = je.Id;
        }
        else
        {
            supplier.Balance -= baseTotal;
            var lines = new List<JournalLineInput>
            {
                new(SystemAccountKeys.AccountsPayable, baseTotal, 0m, $"AP Return {supplier.Name}"),
                new(SystemAccountKeys.RawMaterialInventory, 0m, baseSubTotal, $"Purchase Return {inv.InvoiceNumber}")
            };
            if (baseTax > 0)
                lines.Add(new(SystemAccountKeys.TaxReceivable, 0m, baseTax, $"Input VAT Reversal {inv.InvoiceNumber}"));

            var je = await _accounting.CreateAndPostAsync(TenantId, inv.InvoiceDate, $"Purchase Return {inv.InvoiceNumber}", nameof(PurchaseInvoice), inv.Id, lines, $"PR-{inv.Id}", ct);
            inv.JournalEntryId = je.Id;
        }

        _db.PurchaseInvoices.Add(inv);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await _audit.LogAsync(TenantId, "Post", nameof(PurchaseInvoice), inv.Id.ToString(), newValues: new { inv.InvoiceNumber, inv.TotalAmount }, ct: ct);

        return Ok(inv);
    }
}


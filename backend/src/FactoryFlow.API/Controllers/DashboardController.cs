using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? productId,
        CancellationToken ct)
    {
        var start = from ?? DateTime.UtcNow.AddMonths(-6);
        var end = to ?? DateTime.UtcNow.AddDays(1);

        var prodOrdersQuery = _db.ProductionOrders.Where(o => o.CreatedAt >= start && o.CreatedAt <= end);
        if (branchId.HasValue) prodOrdersQuery = prodOrdersQuery.Where(o => o.BranchId == branchId);
        if (warehouseId.HasValue) prodOrdersQuery = prodOrdersQuery.Where(o => o.WarehouseId == warehouseId || o.OutputWarehouseId == warehouseId);
        if (productId.HasValue) prodOrdersQuery = prodOrdersQuery.Where(o => o.ProductId == productId);

        var prodOrders = await prodOrdersQuery.Include(o => o.Product).ToListAsync(ct);

        var totalProductionValue = prodOrders
            .Where(o => o.Status == ProductionOrderStatus.Completed)
            .Sum(o => o.ProducedQuantity * (o.Product?.SellingPrice ?? 0m));

        var totalProductionCost = prodOrders
            .Sum(o => o.ActualMaterialCost + o.ActualLaborCost + o.ActualOverheadCost);

        var salesQuery = _db.SalesInvoices.Where(s => s.InvoiceDate >= start && s.InvoiceDate <= end && s.Status != InvoiceStatus.Cancelled);
        if (warehouseId.HasValue) salesQuery = salesQuery.Where(s => s.WarehouseId == warehouseId);
        var sales = await salesQuery.Include(s => s.Items).ToListAsync(ct);

        if (productId.HasValue)
            sales = sales.Where(s => s.Items.Any(i => i.ProductId == productId)).ToList();

        var totalSales = sales.Sum(s => s.IsReturn ? -s.TotalAmount : s.TotalAmount);
        var totalCogs = sales.Sum(s => s.IsReturn ? -s.TotalCogs : s.TotalCogs);

        var purchasesQuery = _db.PurchaseInvoices.Where(p => p.InvoiceDate >= start && p.InvoiceDate <= end && p.Status != InvoiceStatus.Cancelled);
        if (warehouseId.HasValue) purchasesQuery = purchasesQuery.Where(p => p.WarehouseId == warehouseId);
        var totalPurchases = await purchasesQuery.SumAsync(p => p.IsReturn ? -p.TotalAmount : p.TotalAmount, ct);

        var expensesFromGl = await _db.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                && l.JournalEntry.EntryDate >= start && l.JournalEntry.EntryDate <= end
                && l.Account!.AccountType == AccountType.Expense)
            .SumAsync(l => l.Debit - l.Credit, ct);

        var netProfit = totalSales - expensesFromGl;

        var stockQuery = _db.StockBalances.AsQueryable();
        if (warehouseId.HasValue) stockQuery = stockQuery.Where(s => s.WarehouseId == warehouseId);
        var stockBalances = await stockQuery.ToListAsync(ct);

        var rawMaterialValue = stockBalances.Where(s => s.RawMaterialId != null).Sum(s => s.TotalValue);
        var finishedGoodsValue = stockBalances.Where(s => s.ProductId != null).Sum(s => s.TotalValue);

        var rawMaterials = await _db.RawMaterials.ToListAsync(ct);
        var lowStockRawMaterials = rawMaterials
            .Select(rm => new
            {
                rm.Id,
                rm.Code,
                rm.Name,
                rm.NameAr,
                rm.MinimumStockLevel,
                OnHand = stockBalances.Where(b => b.RawMaterialId == rm.Id).Sum(b => b.QuantityOnHand)
            })
            .Where(x => x.OnHand <= x.MinimumStockLevel)
            .ToList();

        var products = await _db.Products.ToListAsync(ct);
        var lowStockProducts = products
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Name,
                p.NameAr,
                p.MinimumStockLevel,
                OnHand = stockBalances.Where(b => b.ProductId == p.Id).Sum(b => b.QuantityOnHand)
            })
            .Where(x => x.OnHand <= x.MinimumStockLevel)
            .ToList();

        var inProgressOrders = prodOrders.Count(o => o.Status is ProductionOrderStatus.InProgress or ProductionOrderStatus.Planned or ProductionOrderStatus.Paused);
        var completedOrders = prodOrders.Count(o => o.Status == ProductionOrderStatus.Completed);
        var defectiveProducts = prodOrders.Sum(o => o.DefectiveQuantity);

        var costComparison = prodOrders.Take(10).Select(o => new
        {
            o.OrderNumber,
            ProductName = o.Product?.Name ?? "",
            PlannedCost = o.PlannedMaterialCost + o.PlannedLaborCost + o.PlannedOverheadCost,
            ActualCost = o.ActualMaterialCost + o.ActualLaborCost + o.ActualOverheadCost
        });

        var monthlyChart = Enumerable.Range(0, 6).Select(i =>
        {
            var m = DateTime.UtcNow.AddMonths(-5 + i);
            var mSales = sales.Where(s => s.InvoiceDate.Year == m.Year && s.InvoiceDate.Month == m.Month).Sum(s => s.TotalAmount);
            var mProd = prodOrders.Where(o => o.CreatedAt.Year == m.Year && o.CreatedAt.Month == m.Month).Sum(o => o.ProducedQuantity);
            var mCost = prodOrders.Where(o => o.CreatedAt.Year == m.Year && o.CreatedAt.Month == m.Month).Sum(o => o.ActualMaterialCost + o.ActualLaborCost + o.ActualOverheadCost);
            return new
            {
                Month = m.ToString("yyyy-MM"),
                Sales = mSales,
                ProductionUnits = mProd,
                ProductionCost = mCost,
                GrossProfit = mSales - totalCogs
            };
        });

        return Ok(new
        {
            totalProductionValue,
            totalProductionCost,
            totalSales,
            totalPurchases,
            totalExpenses = expensesFromGl,
            netProfit,
            rawMaterialValue,
            finishedGoodsValue,
            inProgressOrders,
            completedOrders,
            defectiveProducts,
            lowStockRawMaterials,
            lowStockProducts,
            costComparison,
            monthlyChart
        });
    }
}


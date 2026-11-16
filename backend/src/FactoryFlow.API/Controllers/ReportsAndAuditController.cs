using FactoryFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IApplicationDbContext _db;

    public ReportsController(IApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("production-costing")]
    public async Task<IActionResult> GetProductionCostingReport([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? productId, CancellationToken ct)
    {
        var q = _db.ProductionOrders.Include(o => o.Product).AsQueryable();
        if (from.HasValue) q = q.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(o => o.CreatedAt <= to.Value);
        if (productId.HasValue) q = q.Where(o => o.ProductId == productId);

        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        return Ok(orders.Select(o => new
        {
            o.Id,
            o.OrderNumber,
            ProductName = o.Product?.Name,
            o.Status,
            o.PlannedQuantity,
            o.ProducedQuantity,
            o.DefectiveQuantity,
            PlannedTotalCost = o.PlannedMaterialCost + o.PlannedLaborCost + o.PlannedOverheadCost,
            o.ActualMaterialCost,
            o.ActualLaborCost,
            o.ActualOverheadCost,
            ActualTotalCost = o.ActualMaterialCost + o.ActualLaborCost + o.ActualOverheadCost,
            CostVariance = (o.ActualMaterialCost + o.ActualLaborCost + o.ActualOverheadCost) - (o.PlannedMaterialCost + o.PlannedLaborCost + o.PlannedOverheadCost),
            o.ScrapAndWasteCost,
            o.ActualUnitCost,
            SellingPrice = o.Product?.SellingPrice ?? 0m,
            EstimatedMargin = (o.Product?.SellingPrice ?? 0m) - o.ActualUnitCost
        }));
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(CancellationToken ct) =>
        Ok(await _db.Notifications.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync(ct));

    [HttpPatch("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n != null) { n.IsRead = true; await _db.SaveChangesAsync(ct); }
        return Ok();
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(CancellationToken ct) =>
        Ok(await _db.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(200).ToListAsync(ct));
}


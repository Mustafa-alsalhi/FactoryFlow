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
public class ManufacturingController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IInventoryService _inventory;
    private readonly IAccountingService _accounting;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    public ManufacturingController(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IInventoryService inventory,
        IAccountingService accounting,
        IAuditService audit,
        INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _inventory = inventory;
        _accounting = accounting;
        _audit = audit;
        _notifications = notifications;
    }

    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException();

    // --- BOM ---
    [HttpGet("boms")]
    public async Task<IActionResult> GetBoms(CancellationToken ct) =>
        Ok(await _db.BillsOfMaterials
            .Include(b => b.Product)
            .Include(b => b.Items).ThenInclude(i => i.RawMaterial)
            .Include(b => b.Items).ThenInclude(i => i.UnitOfMeasure)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct));

    [HttpPost("boms")]
    public async Task<IActionResult> CreateBom([FromBody] BillOfMaterials bom, CancellationToken ct)
    {
        bom.Id = Guid.NewGuid();
        bom.TenantId = TenantId;
        decimal matCost = 0m;
        foreach (var item in bom.Items)
        {
            item.Id = Guid.NewGuid();
            item.TenantId = TenantId;
            item.BomId = bom.Id;
            var rm = await _db.RawMaterials.FirstOrDefaultAsync(r => r.Id == item.RawMaterialId, ct);
            item.UnitCost = rm?.StandardCost ?? item.UnitCost;
            var effectiveQty = item.Quantity * (1m + (item.WastePercentage / 100m));
            item.TotalCost = Math.Round(effectiveQty * item.UnitCost, 4);
            matCost += item.TotalCost;
        }
        bom.EstimatedMaterialCost = Math.Round(matCost, 4);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync(ct);
        return Ok(bom);
    }

    [HttpGet("boms/{id:guid}/calculate-cost")]
    public async Task<IActionResult> CalculateBomCost(Guid id, [FromQuery] decimal quantity = 1m, CancellationToken ct = default)
    {
        var bom = await _db.BillsOfMaterials
            .Include(b => b.Items).ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new KeyNotFoundException("BOM not found.");

        var factor = quantity / Math.Max(1m, bom.OutputQuantity);
        var materials = bom.Items.Select(i =>
        {
            var reqQty = Math.Round(i.Quantity * factor * (1m + i.WastePercentage / 100m), 4);
            var cost = Math.Round(reqQty * i.UnitCost, 4);
            return new { i.RawMaterialId, MaterialName = i.RawMaterial?.Name, RequiredQuantity = reqQty, i.UnitCost, TotalCost = cost };
        }).ToList();

        var totalMaterial = materials.Sum(m => m.TotalCost);
        var totalLabor = Math.Round(bom.EstimatedLaborCost * factor, 4);
        var totalOverhead = Math.Round(bom.EstimatedOverheadCost * factor, 4);
        var grandTotal = totalMaterial + totalLabor + totalOverhead;

        return Ok(new
        {
            bom.Id,
            bom.Code,
            TargetQuantity = quantity,
            TotalMaterialCost = totalMaterial,
            TotalLaborCost = totalLabor,
            TotalOverheadCost = totalOverhead,
            TotalEstimatedCost = grandTotal,
            EstimatedUnitCost = quantity > 0 ? Math.Round(grandTotal / quantity, 4) : 0m,
            Materials = materials
        });
    }

    // --- Lines, Machines, Employees ---
    [HttpGet("lines")]
    public async Task<IActionResult> GetLines(CancellationToken ct) =>
        Ok(await _db.ProductionLines.Include(l => l.Machines).ToListAsync(ct));

    [HttpPost("lines")]
    public async Task<IActionResult> CreateLine([FromBody] ProductionLine line, CancellationToken ct)
    {
        line.Id = Guid.NewGuid();
        line.TenantId = TenantId;
        _db.ProductionLines.Add(line);
        await _db.SaveChangesAsync(ct);
        return Ok(line);
    }

    [HttpGet("machines")]
    public async Task<IActionResult> GetMachines(CancellationToken ct) =>
        Ok(await _db.Machines.Include(m => m.ProductionLine).Include(m => m.MaintenanceLogs).ToListAsync(ct));

    [HttpPost("machines")]
    public async Task<IActionResult> CreateMachine([FromBody] Machine m, CancellationToken ct)
    {
        m.Id = Guid.NewGuid();
        m.TenantId = TenantId;
        _db.Machines.Add(m);
        await _db.SaveChangesAsync(ct);
        return Ok(m);
    }

    [HttpPost("machines/{id:guid}/maintenance")]
    public async Task<IActionResult> AddMaintenance(Guid id, [FromBody] MachineMaintenance log, CancellationToken ct)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new KeyNotFoundException("Machine not found.");
        log.Id = Guid.NewGuid();
        log.TenantId = TenantId;
        log.MachineId = id;
        machine.TotalDowntimeHours += log.DowntimeHours;
        _db.MachineMaintenances.Add(log);
        await _db.SaveChangesAsync(ct);
        return Ok(log);
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees(CancellationToken ct) =>
        Ok(await _db.Employees.OrderBy(e => e.Code).ToListAsync(ct));

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] Employee emp, CancellationToken ct)
    {
        emp.Id = Guid.NewGuid();
        emp.TenantId = TenantId;
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync(ct);
        return Ok(emp);
    }

    // --- Production Orders ---
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(CancellationToken ct) =>
        Ok(await _db.ProductionOrders
            .Include(o => o.Product)
            .Include(o => o.Bom)
            .Include(o => o.ProductionLine)
            .Include(o => o.Supervisor)
            .Include(o => o.MaterialIssues).ThenInclude(m => m.RawMaterial)
            .Include(o => o.LaborLogs).ThenInclude(l => l.Employee)
            .Include(o => o.OverheadLogs)
            .Include(o => o.Outputs)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct));

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] ProductionOrder order, CancellationToken ct)
    {
        var bom = await _db.BillsOfMaterials.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == order.BomId, ct)
            ?? throw new KeyNotFoundException("BOM not found.");

        var count = await _db.ProductionOrders.IgnoreQueryFilters().CountAsync(o => o.TenantId == TenantId, ct);
        order.Id = Guid.NewGuid();
        order.TenantId = TenantId;
        order.OrderNumber = $"MO-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
        order.Status = ProductionOrderStatus.Planned;

        var factor = order.PlannedQuantity / Math.Max(1m, bom.OutputQuantity);
        order.PlannedMaterialCost = Math.Round(bom.EstimatedMaterialCost * factor, 4);
        order.PlannedLaborCost = Math.Round(bom.EstimatedLaborCost * factor, 4);
        order.PlannedOverheadCost = Math.Round(bom.EstimatedOverheadCost * factor, 4);

        foreach (var item in bom.Items)
        {
            order.MaterialIssues.Add(new ProductionMaterialIssue
            {
                TenantId = TenantId,
                ProductionOrderId = order.Id,
                RawMaterialId = item.RawMaterialId,
                WarehouseId = order.WarehouseId,
                PlannedQuantity = Math.Round(item.Quantity * factor * (1m + item.WastePercentage / 100m), 4),
                UnitCost = item.UnitCost
            });
        }

        _db.ProductionOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(TenantId, "Create", nameof(ProductionOrder), order.Id.ToString(), newValues: new { order.OrderNumber, order.PlannedQuantity }, ct: ct);
        return Ok(order);
    }

    [HttpPost("orders/{id:guid}/start")]
    public async Task<IActionResult> StartOrder(Guid id, CancellationToken ct)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new KeyNotFoundException("Order not found.");
        if (order.Status is not (ProductionOrderStatus.Draft or ProductionOrderStatus.Planned or ProductionOrderStatus.Paused))
            throw new DomainException("Order cannot be started from current status.");

        order.Status = ProductionOrderStatus.InProgress;
        order.ActualStartDate ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(order);
    }

    [HttpPost("orders/{id:guid}/pause")]
    public async Task<IActionResult> PauseOrder(Guid id, CancellationToken ct)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new KeyNotFoundException("Order not found.");
        if (order.Status != ProductionOrderStatus.InProgress) throw new DomainException("Only InProgress orders can be paused.");
        order.Status = ProductionOrderStatus.Paused;
        await _db.SaveChangesAsync(ct);
        return Ok(order);
    }

    public record IssueMaterialRequest(Guid RawMaterialId, Guid WarehouseId, decimal Quantity, decimal WasteQuantity = 0m);

    [HttpPost("orders/{id:guid}/issue-material")]
    public async Task<IActionResult> IssueMaterial(Guid id, [FromBody] IssueMaterialRequest req, CancellationToken ct)
    {
        var order = await _db.ProductionOrders.Include(o => o.MaterialIssues).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new KeyNotFoundException("Order not found.");
        if (order.Status != ProductionOrderStatus.InProgress)
            throw new DomainException("Materials can only be issued to an InProgress production order.");

        await using var tx = await _db.BeginTransactionAsync(ct);
        var invTx = await _inventory.IssueAsync(TenantId, req.WarehouseId, req.RawMaterialId, null, req.Quantity,
            StockTransactionType.ProductionIssue, nameof(ProductionOrder), order.Id, $"Issued to {order.OrderNumber}", null, ct);

        var line = order.MaterialIssues.FirstOrDefault(m => m.RawMaterialId == req.RawMaterialId);
        if (line == null)
        {
            line = new ProductionMaterialIssue
            {
                TenantId = TenantId,
                ProductionOrderId = order.Id,
                RawMaterialId = req.RawMaterialId,
                WarehouseId = req.WarehouseId
            };
            _db.ProductionMaterialIssues.Add(line);
        }

        line.IssuedQuantity += req.Quantity;
        line.ConsumedQuantity += req.Quantity;
        line.WasteQuantity += req.WasteQuantity;
        line.UnitCost = invTx.UnitCost;
        line.TotalCost += invTx.TotalCost;

        order.ActualMaterialCost += invTx.TotalCost;
        order.ScrapAndWasteCost += Math.Round(req.WasteQuantity * invTx.UnitCost, 4);

        await _accounting.CreateAndPostAsync(
            TenantId,
            DateTime.UtcNow,
            $"Material issue to {order.OrderNumber}",
            nameof(ProductionOrder),
            order.Id,
            new List<JournalLineInput>
            {
                new(SystemAccountKeys.WorkInProgress, invTx.TotalCost, 0m, $"WIP Material {order.OrderNumber}"),
                new(SystemAccountKeys.RawMaterialInventory, 0m, invTx.TotalCost, $"Raw Material Issue {order.OrderNumber}")
            },
            $"MO-MAT-{invTx.Id}", ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(order);
    }

    public record LogLaborRequest(Guid EmployeeId, decimal RegularHours, decimal OvertimeHours, decimal Bonuses, decimal Deductions);

    [HttpPost("orders/{id:guid}/log-labor")]
    public async Task<IActionResult> LogLabor(Guid id, [FromBody] LogLaborRequest req, CancellationToken ct)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new KeyNotFoundException("Order not found.");
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == req.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");

        decimal hourlyRate = emp.WageType switch
        {
            WageType.Hourly => emp.BaseWageRate,
            WageType.Daily => Math.Round(emp.BaseWageRate / 8m, 4),
            WageType.Monthly => Math.Round(emp.BaseWageRate / 208m, 4),
            _ => emp.BaseWageRate
        };
        decimal otRate = emp.OvertimeHourlyRate > 0 ? emp.OvertimeHourlyRate : Math.Round(hourlyRate * 1.5m, 4);
        decimal totalCost = Math.Max(0m, Math.Round((req.RegularHours * hourlyRate) + (req.OvertimeHours * otRate) + req.Bonuses - req.Deductions, 4));

        await using var tx = await _db.BeginTransactionAsync(ct);
        var log = new ProductionLaborLog
        {
            TenantId = TenantId,
            ProductionOrderId = order.Id,
            EmployeeId = emp.Id,
            WorkDate = DateTime.UtcNow,
            RegularHours = req.RegularHours,
            OvertimeHours = req.OvertimeHours,
            Bonuses = req.Bonuses,
            Deductions = req.Deductions,
            TotalLaborCost = totalCost
        };
        _db.ProductionLaborLogs.Add(log);
        order.ActualLaborCost += totalCost;

        if (totalCost > 0)
        {
            await _accounting.CreateAndPostAsync(
                TenantId,
                DateTime.UtcNow,
                $"Direct labor for {order.OrderNumber}",
                nameof(ProductionOrder),
                order.Id,
                new List<JournalLineInput>
                {
                    new(SystemAccountKeys.WorkInProgress, totalCost, 0m, $"WIP Labor {order.OrderNumber}"),
                    new(SystemAccountKeys.DirectLaborExpense, 0m, totalCost, $"Labor Applied {order.OrderNumber}")
                },
                $"MO-LAB-{log.Id}", ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(log);
    }

    public record LogOverheadRequest(Guid? MachineId, string Category, decimal MachineHours, decimal Amount, string? Notes);

    [HttpPost("orders/{id:guid}/log-overhead")]
    public async Task<IActionResult> LogOverhead(Guid id, [FromBody] LogOverheadRequest req, CancellationToken ct)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new KeyNotFoundException("Order not found.");

        await using var tx = await _db.BeginTransactionAsync(ct);
        decimal amount = req.Amount;
        if (req.MachineId.HasValue && req.MachineHours > 0)
        {
            var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == req.MachineId.Value, ct);
            if (machine != null)
            {
                machine.TotalRunHours += req.MachineHours;
                if (amount <= 0) amount = Math.Round(req.MachineHours * machine.HourlyOperatingCost, 4);
            }
        }

        var log = new ProductionOverheadLog
        {
            TenantId = TenantId,
            ProductionOrderId = order.Id,
            MachineId = req.MachineId,
            Category = req.Category,
            MachineHours = req.MachineHours,
            Amount = amount,
            Notes = req.Notes
        };
        _db.ProductionOverheadLogs.Add(log);
        order.ActualOverheadCost += amount;

        if (amount > 0)
        {
            await _accounting.CreateAndPostAsync(
                TenantId,
                DateTime.UtcNow,
                $"Manufacturing overhead ({req.Category}) for {order.OrderNumber}",
                nameof(ProductionOrder),
                order.Id,
                new List<JournalLineInput>
                {
                    new(SystemAccountKeys.WorkInProgress, amount, 0m, $"WIP Overhead {order.OrderNumber}"),
                    new(SystemAccountKeys.ManufacturingOverhead, 0m, amount, $"Overhead Applied {order.OrderNumber}")
                },
                $"MO-OVH-{log.Id}", ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(log);
    }

    public record CompleteOrderRequest(decimal GoodQuantity, decimal DefectiveQuantity, string? BatchNumber);

    [HttpPost("orders/{id:guid}/complete")]
    public async Task<IActionResult> CompleteOrder(Guid id, [FromBody] CompleteOrderRequest req, CancellationToken ct)
    {
        if (req.GoodQuantity <= 0) throw new DomainException("Good quantity must be greater than zero.");

        var order = await _db.ProductionOrders.Include(o => o.Product).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new KeyNotFoundException("Order not found.");
        if (order.Status != ProductionOrderStatus.InProgress)
            throw new DomainException("Only InProgress orders can be completed.");

        await using var tx = await _db.BeginTransactionAsync(ct);

        decimal totalWipCost = order.ActualMaterialCost + order.ActualLaborCost + order.ActualOverheadCost;
        decimal totalUnits = req.GoodQuantity + req.DefectiveQuantity;
        decimal unitCost = totalUnits > 0 ? Math.Round(totalWipCost / req.GoodQuantity, 4) : 0m;
        decimal defectCost = totalUnits > 0 ? Math.Round(totalWipCost * (req.DefectiveQuantity / totalUnits), 4) : 0m;
        decimal goodCost = Math.Max(0m, totalWipCost - defectCost);
        decimal goodUnitCost = Math.Round(goodCost / req.GoodQuantity, 4);

        order.ProducedQuantity = req.GoodQuantity;
        order.DefectiveQuantity = req.DefectiveQuantity;
        order.ActualUnitCost = goodUnitCost;
        order.ScrapAndWasteCost += defectCost;
        order.ActualEndDate = DateTime.UtcNow;
        order.Status = ProductionOrderStatus.Completed;

        if (order.Product != null && goodUnitCost > 0)
            order.Product.StandardCost = goodUnitCost;

        _db.ProductionOutputs.Add(new ProductionOutput
        {
            TenantId = TenantId,
            ProductionOrderId = order.Id,
            OutputDate = DateTime.UtcNow,
            GoodQuantity = req.GoodQuantity,
            DefectiveQuantity = req.DefectiveQuantity,
            BatchNumber = req.BatchNumber ?? order.OrderNumber,
            UnitCost = goodUnitCost,
            TotalCost = goodCost
        });

        await _inventory.ReceiveAsync(
            TenantId,
            order.OutputWarehouseId,
            null,
            order.ProductId,
            req.GoodQuantity,
            goodUnitCost,
            StockTransactionType.ProductionOutput,
            req.BatchNumber ?? order.OrderNumber,
            null,
            nameof(ProductionOrder),
            order.Id,
            $"Completed {order.OrderNumber}",
            $"MO-OUT-{order.Id}", ct);

        if (totalWipCost > 0)
        {
            var lines = new List<JournalLineInput>
            {
                new(SystemAccountKeys.FinishedGoodsInventory, goodCost, 0m, $"Finished Goods {order.OrderNumber}"),
                new(SystemAccountKeys.WorkInProgress, 0m, totalWipCost, $"Clear WIP {order.OrderNumber}")
            };
            if (defectCost > 0)
                lines.Add(new(SystemAccountKeys.ScrapLoss, defectCost, 0m, $"Defective Units Loss {order.OrderNumber}"));

            await _accounting.CreateAndPostAsync(TenantId, DateTime.UtcNow, $"Completion of {order.OrderNumber}", nameof(ProductionOrder), order.Id, lines, $"MO-FIN-{order.Id}", ct);
        }

        var plannedTotal = order.PlannedMaterialCost + order.PlannedLaborCost + order.PlannedOverheadCost;
        if (plannedTotal > 0 && totalWipCost > plannedTotal * 1.1m)
        {
            await _notifications.CreateAsync(TenantId, NotificationType.CostOverrun,
                $"Cost Overrun on {order.OrderNumber}",
                $"تجاوز تكلفة الإنتاج في الأمر {order.OrderNumber}",
                $"Actual cost ({totalWipCost:N2}) exceeded planned cost ({plannedTotal:N2}) by >10%.",
                $"التكلفة الفعلية ({totalWipCost:N2}) تجاوزت المخططة ({plannedTotal:N2}) بأكثر من 10%.",
                nameof(ProductionOrder), order.Id, ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(order);
    }

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken ct)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new KeyNotFoundException("Order not found.");
        if (order.Status == ProductionOrderStatus.Completed)
            throw new DomainException("Completed orders cannot be cancelled.");
        if (order.ActualMaterialCost > 0)
            throw new DomainException("Cannot cancel order with unreturned issued materials. Return materials first.");

        order.Status = ProductionOrderStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return Ok(order);
    }
}


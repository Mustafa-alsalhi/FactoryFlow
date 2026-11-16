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
public class MasterDataController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IInventoryService _inventory;
    private readonly IAuditService _audit;

    public MasterDataController(IApplicationDbContext db, ICurrentUserService currentUser, IInventoryService inventory, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _inventory = inventory;
        _audit = audit;
    }

    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException();

    // Units & Categories
    [HttpGet("units")]
    public async Task<IActionResult> GetUnits(CancellationToken ct) =>
        Ok(await _db.UnitsOfMeasure.OrderBy(u => u.Code).ToListAsync(ct));

    [HttpPost("units")]
    public async Task<IActionResult> CreateUnit([FromBody] UnitOfMeasure uom, CancellationToken ct)
    {
        uom.Id = Guid.NewGuid();
        uom.TenantId = TenantId;
        _db.UnitsOfMeasure.Add(uom);
        await _db.SaveChangesAsync(ct);
        return Ok(uom);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct) =>
        Ok(await _db.Categories.OrderBy(c => c.Code).ToListAsync(ct));

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] Category cat, CancellationToken ct)
    {
        cat.Id = Guid.NewGuid();
        cat.TenantId = TenantId;
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync(ct);
        return Ok(cat);
    }

    // Raw Materials
    [HttpGet("raw-materials")]
    public async Task<IActionResult> GetRawMaterials([FromQuery] string? search, CancellationToken ct)
    {
        var query = _db.RawMaterials.Include(r => r.Category).Include(r => r.UnitOfMeasure).Include(r => r.PreferredSupplier).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.Name.Contains(search) || r.Code.Contains(search) || (r.NameAr != null && r.NameAr.Contains(search)));

        var list = await query.OrderBy(r => r.Code).ToListAsync(ct);
        var balances = await _db.StockBalances.Where(b => b.RawMaterialId != null).ToListAsync(ct);

        var result = list.Select(r => new
        {
            r.Id, r.Code, r.Name, r.NameAr, r.Description, r.CategoryId,
            CategoryName = r.Category?.Name,
            r.UnitOfMeasureId,
            UnitCode = r.UnitOfMeasure?.Code,
            r.PreferredSupplierId,
            SupplierName = r.PreferredSupplier?.Name,
            r.StandardCost, r.LastPurchasePrice, r.MinimumStockLevel,
            r.TrackBatch, r.TrackExpiry, r.IsActive,
            QuantityOnHand = balances.Where(b => b.RawMaterialId == r.Id).Sum(b => b.QuantityOnHand),
            StockValue = balances.Where(b => b.RawMaterialId == r.Id).Sum(b => b.TotalValue)
        });
        return Ok(result);
    }

    [HttpPost("raw-materials")]
    public async Task<IActionResult> CreateRawMaterial([FromBody] RawMaterial rm, CancellationToken ct)
    {
        rm.Id = Guid.NewGuid();
        rm.TenantId = TenantId;
        _db.RawMaterials.Add(rm);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(TenantId, "Create", nameof(RawMaterial), rm.Id.ToString(), newValues: new { rm.Code, rm.Name }, ct: ct);
        return Ok(rm);
    }

    [HttpPut("raw-materials/{id:guid}")]
    public async Task<IActionResult> UpdateRawMaterial(Guid id, [FromBody] RawMaterial req, CancellationToken ct)
    {
        var rm = await _db.RawMaterials.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException("Raw material not found.");
        rm.Name = req.Name;
        rm.NameAr = req.NameAr;
        rm.Description = req.Description;
        rm.CategoryId = req.CategoryId;
        rm.UnitOfMeasureId = req.UnitOfMeasureId;
        rm.PreferredSupplierId = req.PreferredSupplierId;
        rm.StandardCost = req.StandardCost;
        rm.MinimumStockLevel = req.MinimumStockLevel;
        rm.TrackBatch = req.TrackBatch;
        rm.TrackExpiry = req.TrackExpiry;
        rm.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(rm);
    }

    [HttpDelete("raw-materials/{id:guid}")]
    public async Task<IActionResult> DeleteRawMaterial(Guid id, CancellationToken ct)
    {
        var hasStock = await _db.StockBalances.AnyAsync(b => b.RawMaterialId == id && b.QuantityOnHand > 0, ct);
        if (hasStock) throw new DomainException("Cannot delete raw material with positive stock balance.");

        var rm = await _db.RawMaterials.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException("Not found.");
        rm.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return Ok(new { deleted = true });
    }

    // Products
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] string? search, CancellationToken ct)
    {
        var query = _db.Products.Include(p => p.Category).Include(p => p.UnitOfMeasure).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search) || (p.Barcode != null && p.Barcode.Contains(search)));

        var list = await query.OrderBy(p => p.Sku).ToListAsync(ct);
        var balances = await _db.StockBalances.Where(b => b.ProductId != null).ToListAsync(ct);

        return Ok(list.Select(p => new
        {
            p.Id, p.Sku, p.Barcode, p.Name, p.NameAr, p.Description, p.Specifications, p.ImageUrl,
            p.ProductType, p.CategoryId, CategoryName = p.Category?.Name,
            p.UnitOfMeasureId, UnitCode = p.UnitOfMeasure?.Code,
            p.SellingPrice, p.StandardCost, p.MinimumStockLevel, p.IsActive,
            QuantityOnHand = balances.Where(b => b.ProductId == p.Id).Sum(b => b.QuantityOnHand),
            StockValue = balances.Where(b => b.ProductId == p.Id).Sum(b => b.TotalValue)
        }));
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] Product prod, CancellationToken ct)
    {
        prod.Id = Guid.NewGuid();
        prod.TenantId = TenantId;
        _db.Products.Add(prod);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(TenantId, "Create", nameof(Product), prod.Id.ToString(), newValues: new { prod.Sku, prod.Name }, ct: ct);
        return Ok(prod);
    }

    [HttpPut("products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] Product req, CancellationToken ct)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Product not found.");
        p.Sku = req.Sku;
        p.Barcode = req.Barcode;
        p.Name = req.Name;
        p.NameAr = req.NameAr;
        p.Description = req.Description;
        p.Specifications = req.Specifications;
        p.ImageUrl = req.ImageUrl;
        p.ProductType = req.ProductType;
        p.CategoryId = req.CategoryId;
        p.UnitOfMeasureId = req.UnitOfMeasureId;
        p.SellingPrice = req.SellingPrice;
        p.StandardCost = req.StandardCost;
        p.MinimumStockLevel = req.MinimumStockLevel;
        p.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(p);
    }

    // Warehouses & Stock Operations
    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses(CancellationToken ct) =>
        Ok(await _db.Warehouses.Include(w => w.Branch).OrderBy(w => w.Code).ToListAsync(ct));

    [HttpPost("warehouses")]
    public async Task<IActionResult> CreateWarehouse([FromBody] Warehouse wh, CancellationToken ct)
    {
        wh.Id = Guid.NewGuid();
        wh.TenantId = TenantId;
        _db.Warehouses.Add(wh);
        await _db.SaveChangesAsync(ct);
        return Ok(wh);
    }

    [HttpGet("stock-balances")]
    public async Task<IActionResult> GetStockBalances([FromQuery] Guid? warehouseId, CancellationToken ct)
    {
        var q = _db.StockBalances
            .Include(b => b.Warehouse)
            .Include(b => b.RawMaterial)
            .Include(b => b.Product)
            .AsQueryable();
        if (warehouseId.HasValue) q = q.Where(b => b.WarehouseId == warehouseId);
        return Ok(await q.ToListAsync(ct));
    }

    [HttpGet("inventory-transactions")]
    public async Task<IActionResult> GetInventoryTransactions([FromQuery] Guid? warehouseId, CancellationToken ct)
    {
        var q = _db.InventoryTransactions
            .Include(t => t.Warehouse)
            .Include(t => t.RawMaterial)
            .Include(t => t.Product)
            .AsQueryable();
        if (warehouseId.HasValue) q = q.Where(t => t.WarehouseId == warehouseId);
        return Ok(await q.OrderByDescending(t => t.TransactionDate).Take(200).ToListAsync(ct));
    }

    public record TransferStockRequest(Guid FromWarehouseId, Guid ToWarehouseId, Guid? RawMaterialId, Guid? ProductId, decimal Quantity, string? Notes);

    [HttpPost("stock-transfer")]
    public async Task<IActionResult> TransferStock([FromBody] TransferStockRequest req, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(ct);
        var (outTx, inTx) = await _inventory.TransferAsync(TenantId, req.FromWarehouseId, req.ToWarehouseId, req.RawMaterialId, req.ProductId, req.Quantity, req.Notes, ct);
        await tx.CommitAsync(ct);
        return Ok(new { outTx, inTx });
    }

    public record AdjustStockRequest(Guid WarehouseId, Guid? RawMaterialId, Guid? ProductId, decimal Quantity, decimal UnitCost, bool IsIncrease, string? Notes);

    [HttpPost("stock-adjust")]
    public async Task<IActionResult> AdjustStock([FromBody] AdjustStockRequest req, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(ct);
        var result = req.IsIncrease
            ? await _inventory.ReceiveAsync(TenantId, req.WarehouseId, req.RawMaterialId, req.ProductId, req.Quantity, req.UnitCost, StockTransactionType.AdjustmentIn, null, null, "StockAdjustment", null, req.Notes, null, ct)
            : await _inventory.IssueAsync(TenantId, req.WarehouseId, req.RawMaterialId, req.ProductId, req.Quantity, StockTransactionType.AdjustmentOut, "StockAdjustment", null, req.Notes, null, ct);
        await tx.CommitAsync(ct);
        return Ok(result);
    }
}


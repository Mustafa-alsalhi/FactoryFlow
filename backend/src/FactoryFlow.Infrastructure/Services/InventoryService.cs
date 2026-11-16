using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly IApplicationDbContext _db;

    public InventoryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<InventoryTransaction> ReceiveAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid? rawMaterialId,
        Guid? productId,
        decimal quantity,
        decimal unitCost,
        StockTransactionType type,
        string? batchNumber,
        DateTime? expiryDate,
        string? referenceType,
        Guid? referenceId,
        string? notes = null,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            throw new DomainException("Receive quantity must be greater than zero.");
        if (unitCost < 0)
            throw new DomainException("Unit cost cannot be negative.");
        if (rawMaterialId == null && productId == null)
            throw new DomainException("Either RawMaterialId or ProductId must be specified.");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.InventoryTransactions
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IdempotencyKey == idempotencyKey, ct);
            if (existing != null)
                return existing;
        }

        var balance = await GetOrCreateBalanceAsync(tenantId, warehouseId, rawMaterialId, productId, ct);

        var addedValue = Math.Round(quantity * unitCost, 4);
        var newQty = balance.QuantityOnHand + quantity;
        var newTotalValue = balance.TotalValue + addedValue;

        balance.QuantityOnHand = newQty;
        balance.TotalValue = newTotalValue;
        balance.AverageUnitCost = newQty > 0 ? Math.Round(newTotalValue / newQty, 4) : unitCost;

        var effectiveBatch = string.IsNullOrWhiteSpace(batchNumber)
            ? $"B-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : batchNumber;

        _db.InventoryBatches.Add(new InventoryBatch
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            RawMaterialId = rawMaterialId,
            ProductId = productId,
            BatchNumber = effectiveBatch,
            ReceivedDate = DateTime.UtcNow,
            ExpiryDate = expiryDate,
            InitialQuantity = quantity,
            RemainingQuantity = quantity,
            UnitCost = unitCost
        });

        var count = await _db.InventoryTransactions.IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == tenantId, ct);

        var tx = new InventoryTransaction
        {
            TenantId = tenantId,
            TransactionNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}",
            TransactionDate = DateTime.UtcNow,
            TransactionType = type,
            WarehouseId = warehouseId,
            RawMaterialId = rawMaterialId,
            ProductId = productId,
            BatchNumber = effectiveBatch,
            ExpiryDate = expiryDate,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = addedValue,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Notes = notes,
            IdempotencyKey = idempotencyKey
        };

        _db.InventoryTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);
        return tx;
    }

    public async Task<InventoryTransaction> IssueAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid? rawMaterialId,
        Guid? productId,
        decimal quantity,
        StockTransactionType type,
        string? referenceType,
        Guid? referenceId,
        string? notes = null,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            throw new DomainException("Issue quantity must be greater than zero.");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.InventoryTransactions
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IdempotencyKey == idempotencyKey, ct);
            if (existing != null)
                return existing;
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new DomainException("Tenant not found.");

        var balance = await GetOrCreateBalanceAsync(tenantId, warehouseId, rawMaterialId, productId, ct);

        if (!tenant.AllowNegativeStock && balance.QuantityOnHand < quantity)
            throw new DomainException($"Insufficient stock. Available: {balance.QuantityOnHand}, Requested: {quantity}");

        decimal totalCost;
        decimal effectiveUnitCost;

        if (tenant.ValuationMethod == InventoryValuationMethod.FIFO)
        {
            var batches = await _db.InventoryBatches
                .Where(b => b.TenantId == tenantId
                    && b.WarehouseId == warehouseId
                    && b.RawMaterialId == rawMaterialId
                    && b.ProductId == productId
                    && b.RemainingQuantity > 0)
                .OrderBy(b => b.ReceivedDate)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(ct);

            decimal remainingToIssue = quantity;
            decimal accumulatedCost = 0m;

            foreach (var batch in batches)
            {
                if (remainingToIssue <= 0) break;
                var take = Math.Min(batch.RemainingQuantity, remainingToIssue);
                batch.RemainingQuantity -= take;
                accumulatedCost += Math.Round(take * batch.UnitCost, 4);
                remainingToIssue -= take;
            }

            if (remainingToIssue > 0)
            {
                // Fallback if negative stock allowed
                accumulatedCost += Math.Round(remainingToIssue * balance.AverageUnitCost, 4);
            }

            totalCost = Math.Round(accumulatedCost, 4);
            effectiveUnitCost = Math.Round(totalCost / quantity, 4);
        }
        else
        {
            // Weighted Average
            effectiveUnitCost = balance.AverageUnitCost;
            totalCost = Math.Round(quantity * effectiveUnitCost, 4);
        }

        balance.QuantityOnHand -= quantity;
        balance.TotalValue = Math.Max(0, balance.TotalValue - totalCost);

        var count = await _db.InventoryTransactions.IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == tenantId, ct);

        var tx = new InventoryTransaction
        {
            TenantId = tenantId,
            TransactionNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}",
            TransactionDate = DateTime.UtcNow,
            TransactionType = type,
            WarehouseId = warehouseId,
            RawMaterialId = rawMaterialId,
            ProductId = productId,
            Quantity = quantity,
            UnitCost = effectiveUnitCost,
            TotalCost = totalCost,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Notes = notes,
            IdempotencyKey = idempotencyKey
        };

        _db.InventoryTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);
        return tx;
    }

    public async Task<(InventoryTransaction OutTx, InventoryTransaction InTx)> TransferAsync(
        Guid tenantId,
        Guid fromWarehouseId,
        Guid toWarehouseId,
        Guid? rawMaterialId,
        Guid? productId,
        decimal quantity,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (fromWarehouseId == toWarehouseId)
            throw new DomainException("Source and destination warehouses must be different.");

        var outTx = await IssueAsync(tenantId, fromWarehouseId, rawMaterialId, productId, quantity,
            StockTransactionType.TransferOut, "WarehouseTransfer", toWarehouseId, notes, null, ct);

        var inTx = await ReceiveAsync(tenantId, toWarehouseId, rawMaterialId, productId, quantity,
            outTx.UnitCost, StockTransactionType.TransferIn, outTx.BatchNumber, null, "WarehouseTransfer", fromWarehouseId, notes, null, ct);

        return (outTx, inTx);
    }

    private async Task<StockBalance> GetOrCreateBalanceAsync(Guid tenantId, Guid warehouseId, Guid? rawMaterialId, Guid? productId, CancellationToken ct)
    {
        var balance = await _db.StockBalances.FirstOrDefaultAsync(b =>
            b.TenantId == tenantId &&
            b.WarehouseId == warehouseId &&
            b.RawMaterialId == rawMaterialId &&
            b.ProductId == productId, ct);

        if (balance == null)
        {
            balance = new StockBalance
            {
                TenantId = tenantId,
                WarehouseId = warehouseId,
                RawMaterialId = rawMaterialId,
                ProductId = productId,
                QuantityOnHand = 0m,
                ReservedQuantity = 0m,
                AverageUnitCost = 0m,
                TotalValue = 0m
            };
            _db.StockBalances.Add(balance);
        }

        return balance;
    }
}


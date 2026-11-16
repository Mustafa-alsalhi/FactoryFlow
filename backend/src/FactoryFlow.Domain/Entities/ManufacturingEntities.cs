using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Enums;

namespace FactoryFlow.Domain.Entities;

public class BillOfMaterials : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public decimal OutputQuantity { get; set; } = 1m;
    public decimal EstimatedMaterialCost { get; set; }
    public decimal EstimatedLaborCost { get; set; }
    public decimal EstimatedOverheadCost { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public Product? Product { get; set; }
    public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
}

public class BomItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid BomId { get; set; }
    public Guid RawMaterialId { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public decimal Quantity { get; set; }
    public decimal WastePercentage { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    public BillOfMaterials? Bom { get; set; }
    public RawMaterial? RawMaterial { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
}

public class ProductionLine : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public decimal DailyCapacityHours { get; set; } = 8m;
    public bool IsActive { get; set; } = true;

    public Branch? Branch { get; set; }
    public ICollection<Machine> Machines { get; set; } = new List<Machine>();
}

public class Machine : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductionLineId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public decimal HourlyOperatingCost { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal UsefulLifeHours { get; set; } = 10000m;
    public decimal TotalRunHours { get; set; }
    public decimal TotalDowntimeHours { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductionLine? ProductionLine { get; set; }
    public ICollection<MachineMaintenance> MaintenanceLogs { get; set; } = new List<MachineMaintenance>();
}

public class MachineMaintenance : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MachineId { get; set; }
    public DateTime MaintenanceDate { get; set; }
    public string MaintenanceType { get; set; } = "Preventive"; // Preventive, Breakdown
    public decimal DowntimeHours { get; set; }
    public string? FailureReason { get; set; }
    public decimal Cost { get; set; }
    public string? Notes { get; set; }

    public Machine? Machine { get; set; }
}

public class Employee : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? FullNameAr { get; set; }
    public string Department { get; set; } = "Production";
    public string JobTitle { get; set; } = "Operator";
    public WageType WageType { get; set; } = WageType.Hourly;
    public decimal BaseWageRate { get; set; }
    public decimal OvertimeHourlyRate { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public Branch? Branch { get; set; }
}

public class ProductionOrder : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid OutputWarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid BomId { get; set; }
    public Guid? ProductionLineId { get; set; }
    public Guid? SupervisorId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal DefectiveQuantity { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Draft;

    // Planned vs Actual Costing
    public decimal PlannedMaterialCost { get; set; }
    public decimal PlannedLaborCost { get; set; }
    public decimal PlannedOverheadCost { get; set; }
    public decimal ActualMaterialCost { get; set; }
    public decimal ActualLaborCost { get; set; }
    public decimal ActualOverheadCost { get; set; }
    public decimal ScrapAndWasteCost { get; set; }
    public decimal ActualUnitCost { get; set; }
    public string? Notes { get; set; }

    public Branch? Branch { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Warehouse? OutputWarehouse { get; set; }
    public Product? Product { get; set; }
    public BillOfMaterials? Bom { get; set; }
    public ProductionLine? ProductionLine { get; set; }
    public Employee? Supervisor { get; set; }

    public ICollection<ProductionMaterialIssue> MaterialIssues { get; set; } = new List<ProductionMaterialIssue>();
    public ICollection<ProductionLaborLog> LaborLogs { get; set; } = new List<ProductionLaborLog>();
    public ICollection<ProductionOverheadLog> OverheadLogs { get; set; } = new List<ProductionOverheadLog>();
    public ICollection<ProductionOutput> Outputs { get; set; } = new List<ProductionOutput>();
    public ICollection<QualityInspection> QualityInspections { get; set; } = new List<QualityInspection>();
}

public class ProductionMaterialIssue : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductionOrderId { get; set; }
    public Guid RawMaterialId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal WasteQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    public ProductionOrder? ProductionOrder { get; set; }
    public RawMaterial? RawMaterial { get; set; }
}

public class ProductionLaborLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductionOrderId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime WorkDate { get; set; }
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public decimal TotalLaborCost { get; set; }

    public ProductionOrder? ProductionOrder { get; set; }
    public Employee? Employee { get; set; }
}

public class ProductionOverheadLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductionOrderId { get; set; }
    public Guid? MachineId { get; set; }
    public string Category { get; set; } = "Electricity"; // Electricity, Water, Rent, Maintenance, Depreciation, Other
    public decimal MachineHours { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }

    public ProductionOrder? ProductionOrder { get; set; }
    public Machine? Machine { get; set; }
}

public class ProductionOutput : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductionOrderId { get; set; }
    public DateTime OutputDate { get; set; }
    public decimal GoodQuantity { get; set; }
    public decimal DefectiveQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    public ProductionOrder? ProductionOrder { get; set; }
}

public class QualityInspection : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string InspectionNumber { get; set; } = string.Empty;
    public QualityInspectionType InspectionType { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public Guid? RawMaterialId { get; set; }
    public Guid? ProductId { get; set; }
    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;
    public decimal InspectedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? DefectReason { get; set; }
    public string? ReworkReason { get; set; }
    public QualityInspectionStatus Status { get; set; }
    public string? InspectorName { get; set; }

    public ProductionOrder? ProductionOrder { get; set; }
    public RawMaterial? RawMaterial { get; set; }
    public Product? Product { get; set; }
}


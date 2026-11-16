using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class QualityController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public QualityController(IApplicationDbContext db, ICurrentUserService currentUser, INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> GetInspections(CancellationToken ct) =>
        Ok(await _db.QualityInspections
            .Include(q => q.ProductionOrder)
            .Include(q => q.RawMaterial)
            .Include(q => q.Product)
            .OrderByDescending(q => q.InspectionDate)
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> CreateInspection([FromBody] QualityInspection qc, CancellationToken ct)
    {
        var count = await _db.QualityInspections.IgnoreQueryFilters().CountAsync(q => q.TenantId == TenantId, ct);
        qc.Id = Guid.NewGuid();
        qc.TenantId = TenantId;
        qc.InspectionNumber = $"QC-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
        qc.InspectorName ??= _currentUser.Email;
        qc.Status = qc.RejectedQuantity == 0
            ? QualityInspectionStatus.Passed
            : qc.AcceptedQuantity > 0
                ? QualityInspectionStatus.Partial
                : QualityInspectionStatus.Failed;

        _db.QualityInspections.Add(qc);
        await _db.SaveChangesAsync(ct);

        if (qc.RejectedQuantity > 0)
        {
            await _notifications.CreateAsync(TenantId, NotificationType.DefectiveProduct,
                $"QC Defect Recorded ({qc.InspectionNumber})",
                $"تم تسجيل عيوب جودة ({qc.InspectionNumber})",
                $"Rejected quantity: {qc.RejectedQuantity}. Reason: {qc.DefectReason}",
                $"الكمية المرفوضة: {qc.RejectedQuantity}. السبب: {qc.DefectReason}",
                nameof(QualityInspection), qc.Id, ct);
        }

        return Ok(qc);
    }
}


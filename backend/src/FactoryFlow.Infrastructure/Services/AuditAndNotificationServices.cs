using System.Text.Json;
using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;

namespace FactoryFlow.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(Guid tenantId, string action, string entityName, string entityId, object? oldValues = null, object? newValues = null, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            TenantId = tenantId,
            UserId = _currentUser.UserId,
            UserEmail = _currentUser.Email,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = _currentUser.IpAddress
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _db;

    public NotificationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(Guid tenantId, NotificationType type, string title, string titleAr, string message, string messageAr, string? referenceEntity = null, Guid? referenceId = null, CancellationToken ct = default)
    {
        _db.Notifications.Add(new Notification
        {
            TenantId = tenantId,
            Type = type,
            Title = title,
            TitleAr = titleAr,
            Message = message,
            MessageAr = messageAr,
            ReferenceEntity = referenceEntity,
            ReferenceId = referenceId
        });
        await _db.SaveChangesAsync(ct);
    }
}


using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Enums;

namespace FactoryFlow.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxNumber { get; set; }
    public string BaseCurrencyCode { get; set; } = "USD";
    public int DecimalPlaces { get; set; } = 2;
    public int FiscalYearStartMonth { get; set; } = 1;
    public InventoryValuationMethod ValuationMethod { get; set; } = InventoryValuationMethod.WeightedAverage;
    public OverheadAllocationMethod OverheadAllocationMethod { get; set; } = OverheadAllocationMethod.LaborHours;
    public bool AllowNegativeStock { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}

public class Branch : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsMain { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}

public class AppUser : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? FullNameAr { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }

    public Branch? Branch { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class AppRole : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

public class UserRole : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public AppUser? User { get; set; }
    public AppRole? Role { get; set; }
}

public class RolePermission : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string Module { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }
    public bool CanPrint { get; set; }
    public bool CanExport { get; set; }
}

public class RefreshToken : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? CreatedByIp { get; set; }
    public bool IsRevoked => RevokedAt != null;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
}

public class LoginLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class AuditLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
}

public class Notification : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageAr { get; set; } = string.Empty;
    public string? ReferenceEntity { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; }
}

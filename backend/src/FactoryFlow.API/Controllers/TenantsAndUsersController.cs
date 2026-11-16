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
public class TenantsAndUsersController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public TenantsAndUsersController(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet("tenant")]
    public async Task<IActionResult> GetTenant(CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantId, ct);
        return Ok(tenant);
    }

    public record UpdateTenantRequest(string Name, string? NameAr, string? Address, string? Phone, string? Email, string? TaxNumber, string? LogoUrl, string BaseCurrencyCode, int DecimalPlaces, int FiscalYearStartMonth, InventoryValuationMethod ValuationMethod, OverheadAllocationMethod OverheadAllocationMethod, bool AllowNegativeStock);

    [HttpPut("tenant")]
    public async Task<IActionResult> UpdateTenant([FromBody] UpdateTenantRequest req, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantId, ct) ?? throw new KeyNotFoundException("Tenant not found");
        tenant.Name = req.Name;
        tenant.NameAr = req.NameAr;
        tenant.Address = req.Address;
        tenant.Phone = req.Phone;
        tenant.Email = req.Email;
        tenant.TaxNumber = req.TaxNumber;
        tenant.LogoUrl = req.LogoUrl;
        tenant.BaseCurrencyCode = req.BaseCurrencyCode;
        tenant.DecimalPlaces = req.DecimalPlaces;
        tenant.FiscalYearStartMonth = req.FiscalYearStartMonth;
        tenant.ValuationMethod = req.ValuationMethod;
        tenant.OverheadAllocationMethod = req.OverheadAllocationMethod;
        tenant.AllowNegativeStock = req.AllowNegativeStock;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(TenantId, "Update", nameof(Tenant), tenant.Id.ToString(), newValues: req, ct: ct);
        return Ok(tenant);
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(CancellationToken ct) =>
        Ok(await _db.Branches.OrderBy(b => b.Code).ToListAsync(ct));

    [HttpPost("branches")]
    public async Task<IActionResult> CreateBranch([FromBody] Branch branch, CancellationToken ct)
    {
        branch.Id = Guid.NewGuid();
        branch.TenantId = TenantId;
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);
        return Ok(branch);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.FullNameAr,
                u.PhoneNumber,
                u.BranchId,
                u.IsActive,
                u.CreatedAt,
                Roles = _db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { r.Id, r.Name })
                    .ToList()
            })
            .ToListAsync(ct);
        return Ok(users);
    }

    public record InviteUserRequest(string Email, string FullName, string? FullNameAr, string? PhoneNumber, Guid? BranchId, Guid RoleId, string Password);

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] InviteUserRequest req, CancellationToken ct)
    {
        var normEmail = req.Email.Trim().ToUpperInvariant();
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normEmail, ct))
            throw new DomainException("User email already exists in this company.");

        var user = new AppUser
        {
            TenantId = TenantId,
            BranchId = req.BranchId,
            Email = req.Email.Trim(),
            NormalizedEmail = normEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            FullName = req.FullName.Trim(),
            FullNameAr = req.FullNameAr,
            PhoneNumber = req.PhoneNumber,
            IsActive = true
        };
        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole { TenantId = TenantId, UserId = user.Id, RoleId = req.RoleId });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(TenantId, "CreateUser", nameof(AppUser), user.Id.ToString(), newValues: new { user.Email, user.FullName }, ct: ct);
        return Ok(new { user.Id, user.Email, user.FullName, user.IsActive });
    }

    [HttpPatch("users/{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new KeyNotFoundException("User not found.");
        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(TenantId, "ToggleActive", nameof(AppUser), user.Id.ToString(), newValues: new { user.IsActive }, ct: ct);
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
    {
        var roles = await _db.Roles.Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync(ct);
        return Ok(roles);
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid roleId, [FromBody] List<RolePermission> permissions, CancellationToken ct)
    {
        var existing = await _db.RolePermissions.Where(p => p.RoleId == roleId).ToListAsync(ct);
        foreach (var item in permissions)
        {
            var match = existing.FirstOrDefault(e => e.Module == item.Module);
            if (match != null)
            {
                match.CanView = item.CanView;
                match.CanCreate = item.CanCreate;
                match.CanEdit = item.CanEdit;
                match.CanDelete = item.CanDelete;
                match.CanApprove = item.CanApprove;
                match.CanPrint = item.CanPrint;
                match.CanExport = item.CanExport;
            }
            else
            {
                item.Id = Guid.NewGuid();
                item.TenantId = TenantId;
                item.RoleId = roleId;
                _db.RolePermissions.Add(item);
            }
        }
        await _db.SaveChangesAsync(ct);
        return Ok(await _db.RolePermissions.Where(p => p.RoleId == roleId).ToListAsync(ct));
    }

    [HttpGet("login-logs")]
    public async Task<IActionResult> GetLoginLogs(CancellationToken ct) =>
        Ok(await _db.LoginLogs.OrderByDescending(l => l.CreatedAt).Take(100).ToListAsync(ct));
}


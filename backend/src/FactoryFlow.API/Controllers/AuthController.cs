using FactoryFlow.Application.Common;
using FactoryFlow.Application.Interfaces;
using FactoryFlow.Domain.Common;
using FactoryFlow.Domain.Entities;
using FactoryFlow.Domain.Enums;
using FactoryFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactoryFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public AuthController(IApplicationDbContext db, IJwtTokenService jwt, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _jwt = jwt;
        _currentUser = currentUser;
        _audit = audit;
    }

    public record RegisterTenantRequest(
        string CompanyCode,
        string CompanyName,
        string? CompanyNameAr,
        string BaseCurrencyCode,
        string OwnerFullName,
        string Email,
        string Password);

    public record LoginRequest(string? TenantCode, string Email, string Password);
    public record RefreshRequest(string RefreshToken);
    public record ForgotPasswordRequest(string? TenantCode, string Email);
    public record ResetPasswordRequest(string Token, string NewPassword);

    [HttpPost("register-tenant")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CompanyCode) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            throw new DomainException("Company code, email, and password are required.");

        var codeUpper = req.CompanyCode.Trim().ToUpperInvariant();
        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Code == codeUpper, ct))
            throw new DomainException("Company code is already registered.");

        await using var tx = await _db.BeginTransactionAsync(ct);

        var tenant = new Tenant
        {
            Code = codeUpper,
            Name = req.CompanyName.Trim(),
            NameAr = req.CompanyNameAr,
            BaseCurrencyCode = string.IsNullOrWhiteSpace(req.BaseCurrencyCode) ? "USD" : req.BaseCurrencyCode.Trim().ToUpperInvariant(),
            Email = req.Email.Trim()
        };
        _db.Tenants.Add(tenant);

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Code = "MAIN",
            Name = "Main Branch",
            NameAr = "الفرع الرئيسي",
            IsMain = true
        };
        _db.Branches.Add(branch);

        var warehouse = new Warehouse
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Code = "WH-01",
            Name = "Main Warehouse",
            NameAr = "المستودع الرئيسي"
        };
        _db.Warehouses.Add(warehouse);

        var ownerRole = new AppRole
        {
            TenantId = tenant.Id,
            Name = DefaultRoles.CompanyOwner,
            NormalizedName = DefaultRoles.CompanyOwner.ToUpperInvariant(),
            Description = "Full access company owner"
        };
        _db.Roles.Add(ownerRole);

        foreach (var roleName in DefaultRoles.All.Where(r => r != DefaultRoles.CompanyOwner))
        {
            _db.Roles.Add(new AppRole
            {
                TenantId = tenant.Id,
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            });
        }

        var user = new AppUser
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Email = req.Email.Trim(),
            NormalizedEmail = req.Email.Trim().ToUpperInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            FullName = req.OwnerFullName.Trim(),
            IsActive = true
        };
        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole { TenantId = tenant.Id, UserId = user.Id, RoleId = ownerRole.Id });

        _db.Currencies.Add(new Currency
        {
            TenantId = tenant.Id,
            Code = tenant.BaseCurrencyCode,
            Name = tenant.BaseCurrencyCode,
            Symbol = tenant.BaseCurrencyCode == "USD" ? "$" : tenant.BaseCurrencyCode,
            IsBase = true
        });

        // Default Chart of Accounts
        var cashAcc = new Account { TenantId = tenant.Id, Code = "1010", Name = "Cash on Hand", NameAr = "الصندوق", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.Cash };
        var bankAcc = new Account { TenantId = tenant.Id, Code = "1020", Name = "Bank", NameAr = "البنك", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.Bank };
        _db.Accounts.AddRange(
            cashAcc, bankAcc,
            new Account { TenantId = tenant.Id, Code = "1100", Name = "Accounts Receivable", NameAr = "ذمم العملاء", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.AccountsReceivable },
            new Account { TenantId = tenant.Id, Code = "1210", Name = "Raw Materials Inventory", NameAr = "مخزون المواد الخام", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.RawMaterialInventory },
            new Account { TenantId = tenant.Id, Code = "1220", Name = "Work In Progress", NameAr = "الإنتاج تحت التشغيل", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.WorkInProgress },
            new Account { TenantId = tenant.Id, Code = "1230", Name = "Finished Goods Inventory", NameAr = "مخزون المنتجات النهائية", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.FinishedGoodsInventory },
            new Account { TenantId = tenant.Id, Code = "1300", Name = "VAT Receivable", NameAr = "ضريبة المدخلات", AccountType = AccountType.Asset, IsSystemAccount = true, SystemKey = SystemAccountKeys.TaxReceivable },
            new Account { TenantId = tenant.Id, Code = "2010", Name = "Accounts Payable", NameAr = "ذمم الموردين", AccountType = AccountType.Liability, IsSystemAccount = true, SystemKey = SystemAccountKeys.AccountsPayable },
            new Account { TenantId = tenant.Id, Code = "2200", Name = "VAT Payable", NameAr = "ضريبة المخرجات", AccountType = AccountType.Liability, IsSystemAccount = true, SystemKey = SystemAccountKeys.TaxPayable },
            new Account { TenantId = tenant.Id, Code = "3010", Name = "Capital & Retained Earnings", NameAr = "رأس المال والأرباح المبقاة", AccountType = AccountType.Equity, IsSystemAccount = true, SystemKey = SystemAccountKeys.RetainedEarnings },
            new Account { TenantId = tenant.Id, Code = "4010", Name = "Sales Revenue", NameAr = "إيرادات المبيعات", AccountType = AccountType.Revenue, IsSystemAccount = true, SystemKey = SystemAccountKeys.SalesRevenue },
            new Account { TenantId = tenant.Id, Code = "5010", Name = "Cost of Goods Sold", NameAr = "تكلفة المبيعات", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.CostOfGoodsSold },
            new Account { TenantId = tenant.Id, Code = "5100", Name = "Direct Labor", NameAr = "الأجور المباشرة", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.DirectLaborExpense },
            new Account { TenantId = tenant.Id, Code = "5200", Name = "Manufacturing Overhead", NameAr = "التكاليف الصناعية غير المباشرة", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.ManufacturingOverhead },
            new Account { TenantId = tenant.Id, Code = "5300", Name = "Scrap Loss", NameAr = "خسائر التلف", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.ScrapLoss },
            new Account { TenantId = tenant.Id, Code = "5400", Name = "Inventory Adjustment", NameAr = "تسويات المخزون", AccountType = AccountType.Expense, IsSystemAccount = true, SystemKey = SystemAccountKeys.InventoryAdjustment }
        );
        _db.Cashboxes.Add(new Cashbox { TenantId = tenant.Id, BranchId = branch.Id, Code = "CB-01", Name = "Main Cashbox", CurrencyCode = tenant.BaseCurrencyCode, AccountId = cashAcc.Id });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var roles = new[] { DefaultRoles.CompanyOwner };
        var accessToken = _jwt.GenerateAccessToken(user, roles);
        var refreshTokenStr = _jwt.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Token = refreshTokenStr,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = _currentUser.IpAddress
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new { accessToken, refreshToken = refreshTokenStr, tenantId = tenant.Id, tenantCode = tenant.Code });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var normEmail = req.Email.Trim().ToUpperInvariant();
        IQueryable<AppUser> userQuery = _db.Users.IgnoreQueryFilters().Where(u => !u.IsDeleted && u.NormalizedEmail == normEmail);

        if (!string.IsNullOrWhiteSpace(req.TenantCode))
        {
            var codeUpper = req.TenantCode.Trim().ToUpperInvariant();
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Code == codeUpper && t.IsActive, ct);
            if (tenant == null)
                return Unauthorized(new { message = "Invalid company code or credentials." });
            userQuery = userQuery.Where(u => u.TenantId == tenant.Id);
        }

        var user = await userQuery.FirstOrDefaultAsync(ct);
        if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            if (user != null)
            {
                _db.LoginLogs.Add(new LoginLog
                {
                    TenantId = user.TenantId,
                    UserId = user.Id,
                    Email = req.Email,
                    Success = false,
                    FailureReason = "Invalid credentials or inactive account",
                    IpAddress = _currentUser.IpAddress,
                    UserAgent = Request.Headers.UserAgent.ToString()
                });
                await _db.SaveChangesAsync(ct);
            }
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await _db.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id && !ur.IsDeleted)
            .Join(_db.Roles.IgnoreQueryFilters(), ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(user, roles);
        var refreshTokenStr = _jwt.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = refreshTokenStr,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = _currentUser.IpAddress
        });

        _db.LoginLogs.Add(new LoginLog
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Email = user.Email,
            Success = true,
            IpAddress = _currentUser.IpAddress,
            UserAgent = Request.Headers.UserAgent.ToString()
        });

        await _db.SaveChangesAsync(ct);

        var tenantInfo = await _db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == user.TenantId, ct);

        return Ok(new
        {
            accessToken,
            refreshToken = refreshTokenStr,
            user = new { user.Id, user.Email, user.FullName, user.FullNameAr, user.TenantId, user.BranchId, roles },
            tenant = new { tenantInfo.Id, tenantInfo.Code, tenantInfo.Name, tenantInfo.NameAr, tenantInfo.BaseCurrencyCode, tenantInfo.ValuationMethod, tenantInfo.OverheadAllocationMethod }
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var existing = await _db.RefreshTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == req.RefreshToken && !r.IsDeleted, ct);

        if (existing == null || !existing.IsActive)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == existing.UserId && u.IsActive, ct);
        if (user == null)
            return Unauthorized(new { message = "User inactive." });

        var roles = await _db.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id && !ur.IsDeleted)
            .Join(_db.Roles.IgnoreQueryFilters(), ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync(ct);

        var newRefreshStr = _jwt.GenerateRefreshToken();
        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByToken = newRefreshStr;

        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = newRefreshStr,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = _currentUser.IpAddress
        });

        await _db.SaveChangesAsync(ct);

        var newAccessToken = _jwt.GenerateAccessToken(user, roles);
        return Ok(new { accessToken = newAccessToken, refreshToken = newRefreshStr });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == req.RefreshToken, ct);
        if (token != null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new { message = "Logged out." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var normEmail = req.Email.Trim().ToUpperInvariant();
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.NormalizedEmail == normEmail && !u.IsDeleted, ct);
        if (user != null)
        {
            user.PasswordResetToken = Guid.NewGuid().ToString("N");
            user.PasswordResetExpiresAt = DateTime.UtcNow.AddHours(2);
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new { message = "If the email exists, a reset token has been generated." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.PasswordResetToken == req.Token && u.PasswordResetExpiresAt > DateTime.UtcNow, ct);
        if (user == null)
            return BadRequest(new { message = "Invalid or expired reset token." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiresAt = null;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(user.TenantId, "ResetPassword", nameof(AppUser), user.Id.ToString(), ct: ct);
        return Ok(new { message = "Password reset successful." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new UnauthorizedAccessException();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var roleIds = await _db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(ct);
        var roles = await _db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => new { r.Id, r.Name }).ToListAsync(ct);
        var permissions = await _db.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId)).ToListAsync(ct);

        return Ok(new
        {
            user = new { user.Id, user.Email, user.FullName, user.FullNameAr, user.BranchId, roles },
            tenant,
            permissions
        });
    }
}


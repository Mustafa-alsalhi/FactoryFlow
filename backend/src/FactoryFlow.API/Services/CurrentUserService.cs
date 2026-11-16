using System.Security.Claims;
using FactoryFlow.Application.Interfaces;

namespace FactoryFlow.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var val = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");
            return Guid.TryParse(val, out var id) ? id : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var val = _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
            return Guid.TryParse(val, out var id) ? id : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("email");

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    public IReadOnlyList<string> Roles => _httpContextAccessor.HttpContext?.User?
        .FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList() ?? new List<string>();

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}


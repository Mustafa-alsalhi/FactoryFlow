using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FactoryFlow.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FactoryFlow.Infrastructure.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(AppUser user, IEnumerable<string> roles);
    string GenerateRefreshToken();
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(AppUser user, IEnumerable<string> roles)
    {
        var keyStr = _config["Jwt:Key"] ?? "FactoryFlow_Super_Secret_Key_Change_In_Production_2026_Min32Chars!";
        var issuer = _config["Jwt:Issuer"] ?? "FactoryFlow.API";
        var audience = _config["Jwt:Audience"] ?? "FactoryFlow.Client";
        var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var mins) ? mins : 60;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("tenant_id", user.TenantId.ToString()),
            new("full_name", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.BranchId.HasValue)
            claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}


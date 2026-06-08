using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SearchConsoleApp.Core.Auth;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Web.Framework.Auth;

/// <summary>
/// IJwtIssuer somut impl. Customer + DeviceSession'dan access token üretir.
///
/// Claims:
/// - sub:    Customer.EntityId (public Guid)
/// - uid:    Customer.Id (internal long)
/// - sid:    DeviceSession.Id (internal long) — varsa
/// - email:  Customer.Email
/// - role:   Customer.Roles (her birini ayrı claim olarak)
/// - jti:    rastgele unique (replay attack koruması)
/// </summary>
public class JwtIssuer : IJwtIssuer, IScopedService
{
    public const string SessionIdClaimType = "sid";

    private readonly IConfiguration _config;

    public JwtIssuer(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresAt) IssueAccessToken(Customer customer, long? sessionId = null)
    {
        var section = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var minutes = int.Parse(section["ExpiresMinutes"] ?? "60");
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customer.EntityId.ToString()),
            new("uid", customer.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, customer.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (sessionId.HasValue)
        {
            claims.Add(new Claim(SessionIdClaimType, sessionId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(customer.Language))
        {
            claims.Add(new Claim("lang", customer.Language));
        }

        foreach (var role in customer.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        // Permission claim'leri — role'lerden resolve edilir.
        // Kullanım: [Authorize(Policy = "perm:customers.update")] veya
        //          User.HasClaim("perm", "customers.update")
        foreach (var perm in RolePermissions.ResolveForRoles(customer.Roles))
        {
            claims.Add(new Claim("perm", perm));
        }

        var token = new JwtSecurityToken(
            issuer: section["Issuer"],
            audience: section["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

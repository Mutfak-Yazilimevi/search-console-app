using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SearchConsoleApp.Web.Framework.Auth;

/// <summary>
/// Controller action'ı belirli permission gerektirir.
///
/// Kullanım:
///   [HasPermission(Permissions.CustomersUpdate)]
///   public async Task<IActionResult> Update(...) { ... }
///
/// JWT'deki "perm" claim'leri kontrol edilir. Multiple permission ile
/// kullanılırsa **hepsi** gerekir (AND mantığı).
/// Bunlardan biri yeterliyse iki ayrı attribute yerine custom policy yaz.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = $"{PolicyPrefix}{permission}";
    }

    public string Permission { get; }
}

/// <summary>
/// Permission requirement — handler bunu kontrol eder.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

/// <summary>
/// JWT'deki "perm" claim'lerini doğrular.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var hasPermission = context.User.Claims
            .Any(c => c.Type == "perm" && c.Value == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// "perm:customers.update" gibi dinamik policy isimleri için provider.
/// ASP.NET Core static policy registration zorunlu tutuyordu — bu provider
/// runtime'da policy üretir.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}

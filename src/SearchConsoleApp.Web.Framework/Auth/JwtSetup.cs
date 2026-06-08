using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SearchConsoleApp.Web.Framework.Auth;

public static class JwtSetup
{
    /// <summary>
    /// JWT Bearer auth + role policy'leri + permission-based authorization.
    ///
    /// İki kullanım birarada:
    /// - `[Authorize(Policy = "WebUser")]` veya `[Authorize(Policy = "Admin")]` (role-based)
    /// - `[HasPermission(Permissions.CustomersUpdate)]` (permission-based)
    ///
    /// Permission'lar JWT'de "perm" claim'i olarak gelir, role'lerden
    /// `RolePermissions.ResolveForRoles` ile çözülür.
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppJwtAuth(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Options'ı DI'dan resolve edilen IConfiguration ile yapılandır.
        // Inline AddJwtBearer(opt => ...) registration anında config değerlerini
        // yakalardı; WebApplicationFactory gibi senaryolarda test config'i o anda
        // henüz merge edilmemiş olabilir → token'lar yanlış key/issuer ile doğrulanıp
        // 401 dönerdi. Lazy binding JwtIssuer ile aynı (merged) config'i kullanır.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((opt, cfg) =>
            {
                var section = cfg.GetSection("Jwt");
                var key = section["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");

                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = section["Issuer"],
                    ValidAudience = section["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                // SignalR için: query string'den de token kabul et (WebSocket header limit'i)
                opt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                        {
                            ctx.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(o =>
        {
            // WebUser: giriş yapmış herhangi bir kullanıcı (role = "user" veya üst)
            o.AddPolicy(Api.AuthorizationPolicies.WebUser, p =>
                p.RequireAuthenticatedUser()
                 .RequireAssertion(c => c.User.IsInRole("user") || c.User.IsInRole("admin")));

            // Admin: sadece admin rolü
            o.AddPolicy(Api.AuthorizationPolicies.Admin, p =>
                p.RequireAuthenticatedUser().RequireRole("admin"));
        });

        // Permission-based authorization
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}

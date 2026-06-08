using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SearchConsoleApp.Web.Framework.Api;

/// <summary>
/// API versioning stratejisi:
///
/// - URL segment versioning: /api/v{version}/public/...
/// - Default: v1
/// - Yeni breaking change: v2 controller'lar eklenir, eski v1 deprecated tag'lenir
/// - Bir endpoint birden çok version destekleyebilir: [MapToApiVersion("1.0")]
///
/// Caller her zaman version belirtmek zorunda — implicit version yok
/// (route'ta zorunlu).
///
/// Frontend client'ları version'ı config'de tutar (apiRootUrl: /api/v1).
/// Yeni version geçişi atomik değil — frontend ekibi koordine edilmeli.
/// </summary>
[AllowAnonymous]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/[controller]")]
public abstract class PublicApiController : ApiControllerBase { }

[Authorize(Policy = AuthorizationPolicies.WebUser)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/web/[controller]")]
public abstract class WebApiController : ApiControllerBase { }

[Authorize(Policy = AuthorizationPolicies.Admin)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/[controller]")]
public abstract class AdminApiController : ApiControllerBase { }

public static class AuthorizationPolicies
{
    public const string WebUser = "WebUser";
    public const string Admin = "Admin";
}

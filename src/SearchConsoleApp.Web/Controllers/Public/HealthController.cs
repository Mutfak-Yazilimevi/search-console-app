using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Public;

/// <summary>
/// ÖRNEK Public API controller — anonim erişim.
/// Route: /api/public/health
/// Swagger doc: "public"
/// </summary>
public class HealthController : PublicApiController
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", utc = DateTime.UtcNow });
}

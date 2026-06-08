using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Services.Auth;

namespace SearchConsoleApp.Web.Framework.Api;

public static class OAuthProblemResults
{
    public static IActionResult FromGuide(OAuthSetupGuide guide, int statusCode = 503)
    {
        return new ObjectResult(new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.3",
            title = guide.Title,
            status = statusCode,
            detail = guide.Summary,
            code = guide.Code,
            provider = guide.Provider,
            purpose = guide.Purpose,
            setupGuide = guide,
        })
        {
            StatusCode = statusCode,
        };
    }
}

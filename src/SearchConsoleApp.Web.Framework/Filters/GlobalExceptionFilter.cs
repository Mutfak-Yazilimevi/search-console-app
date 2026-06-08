using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace SearchConsoleApp.Web.Framework.Filters;

/// <summary>
/// Tüm beklenmedik hataları yakalar, RFC 7807 ProblemDetails formatında döner.
/// Service'lerde try/catch ile hata yutmayı önler — burada merkezi yönetilir.
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception at {Path}",
            context.HttpContext.Request.Path);

        var (status, title) = context.Exception switch
        {
            UnauthorizedAccessException => (401, "Unauthorized"),
            KeyNotFoundException        => (404, "Not found"),
            ArgumentException           => (400, "Bad request"),
            InvalidOperationException   => (409, "Conflict"),
            _                           => (500, "Internal server error")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = _env.IsDevelopment() ? context.Exception.ToString() : null,
            Instance = context.HttpContext.Request.Path
        };

        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

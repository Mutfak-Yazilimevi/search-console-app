using Microsoft.AspNetCore.Mvc;

namespace SearchConsoleApp.Web.Framework.Api;

/// <summary>
/// Tüm API controller'ları için temel sınıf.
/// [ApiController] davranışı (auto-400, model binding, problem details).
/// Standart action result yardımcıları.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Ok<T>(T data) => base.Ok(new ApiResponse<T>(data));

    protected IActionResult NotFoundResult(string message = "Not found") =>
        Problem(statusCode: 404, title: message);

    protected IActionResult ValidationProblem(IDictionary<string, string[]> errors) =>
        ValidationProblem(new ValidationProblemDetails(errors));
}

/// <summary>
/// Tüm başarılı yanıtlar bu zarfla döner — frontend tarafında tutarlı handling.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; } = true;
    public T Data { get; init; }
    public string? Message { get; init; }

    public ApiResponse(T data, string? message = null)
    {
        Data = data;
        Message = message;
    }
}

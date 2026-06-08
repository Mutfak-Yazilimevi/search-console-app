using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SearchConsoleApp.Services.Auth;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Services.MerchantCenter;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.RateLimiting;

namespace SearchConsoleApp.Web.Controllers.Web;

public record MerchantCenterAuthorizeResponse(string AuthorizeUrl);
public record MerchantCenterCallbackRequest(string Code, string State);
public record MerchantCenterStatusResponse(bool Connected, IList<MerchantCenterAccountDto> Accounts);
public record MerchantCenterAccountDto(string AccountId, string Name, string? WebsiteUrl);
public record StartConnectedProductComplianceRequest(string Url, string? MerchantCenterAccountId);

[Route("api/v{version:apiVersion}/web/merchant-center")]
public class MerchantCenterController : WebApiController
{
    private readonly IMerchantCenterAuthService _authService;
    private readonly IMerchantCenterApiClient _apiClient;
    private readonly IProductComplianceService _complianceService;
    private readonly ICustomerService _customerService;

    public MerchantCenterController(
        IMerchantCenterAuthService authService,
        IMerchantCenterApiClient apiClient,
        IProductComplianceService complianceService,
        ICustomerService customerService)
    {
        _authService = authService;
        _apiClient = apiClient;
        _complianceService = complianceService;
        _customerService = customerService;
    }

    [HttpGet("authorize")]
    public async Task<IActionResult> Authorize([FromQuery] string? returnUrl = null)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        try
        {
            var url = await _authService.BuildAuthorizeUrlAsync(customer.Id, returnUrl ?? "/merchant-center");
            return Ok(new MerchantCenterAuthorizeResponse(url));
        }
        catch (OAuthConfigurationException ex)
        {
            return OAuthProblemResults.FromGuide(ex.Guide);
        }
    }

    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] MerchantCenterCallbackRequest request)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        try
        {
            await _authService.HandleCallbackAsync(customer.Id, request.Code, request.State);
            return Ok(new { ok = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(statusCode: 401, title: ex.Message);
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var connected = await _authService.IsConnectedAsync(customer.Id);
        if (!connected)
            return Ok(new MerchantCenterStatusResponse(false, []));

        var token = await _authService.GetAccessTokenAsync(customer.Id, cancellationToken);
        if (token == null)
            return Ok(new MerchantCenterStatusResponse(false, []));

        var accounts = await _apiClient.ListAccountsAsync(token, cancellationToken);
        var dtos = accounts.Select(a => new MerchantCenterAccountDto(a.AccountId, a.Name, a.WebsiteUrl)).ToList();
        return Ok(new MerchantCenterStatusResponse(true, dtos));
    }

    [HttpDelete]
    public async Task<IActionResult> Disconnect()
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        await _authService.DisconnectAsync(customer.Id);
        return Ok(new { ok = true });
    }

    [HttpPost("compliance")]
    [EnableRateLimiting(RateLimitingSetup.AuditPolicy)]
    public async Task<IActionResult> StartCompliance(
        [FromBody] StartConnectedProductComplianceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = ["URL is required."] });

        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        try
        {
            var run = await _complianceService.StartAsync(
                request.Url, customer.Id, request.MerchantCenterAccountId, cancellationToken);
            return Ok(new
            {
                run.EntityId,
                run.InputUrl,
                run.NormalizedUrl,
                Status = run.Status.ToString(),
                AnalysisMode = run.AnalysisMode.ToString(),
                run.CreatedAt,
                run.MerchantCenterAccountId,
            });
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = [ex.Message] });
        }
    }

    [HttpGet("compliance/runs")]
    public async Task<IActionResult> ListComplianceRuns(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var runs = await _complianceService.ListRecentRunsAsync(customer.Id, limit, cancellationToken);
        return Ok(runs);
    }

    private async Task<Core.Domain.Customers.Customer?> GetCurrentCustomerAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var entityId)) return null;
        return await _customerService.GetCustomerByEntityIdAsync(entityId);
    }
}

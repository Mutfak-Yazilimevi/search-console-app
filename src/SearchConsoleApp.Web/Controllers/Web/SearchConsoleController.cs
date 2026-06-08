using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Services.Audit.SearchConsole;
using SearchConsoleApp.Services.Auth;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Web;

public record SearchConsoleAuthorizeResponse(string AuthorizeUrl);
public record SearchConsoleCallbackRequest(string Code, string State);
public record SearchConsoleStatusResponse(bool Connected, IList<SearchConsolePropertyDto> Properties);
public record SearchConsolePropertyDto(string SiteUrl, string PermissionLevel);

/// <summary>
/// Google Search Console OAuth (webmasters.readonly scope).
/// Route: /api/v1/web/search-console/*
/// </summary>
[Route("api/v{version:apiVersion}/web/search-console")]
public class SearchConsoleController : WebApiController
{
    private readonly ISearchConsoleAuthService _authService;
    private readonly ISearchConsoleApiClient _apiClient;
    private readonly ICustomerService _customerService;

    public SearchConsoleController(
        ISearchConsoleAuthService authService,
        ISearchConsoleApiClient apiClient,
        ICustomerService customerService)
    {
        _authService = authService;
        _apiClient = apiClient;
        _customerService = customerService;
    }

    [HttpGet("authorize")]
    public async Task<IActionResult> Authorize([FromQuery] string? returnUrl = null)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        try
        {
            var url = await _authService.BuildAuthorizeUrlAsync(customer.Id, returnUrl ?? "/");
            return Ok(new SearchConsoleAuthorizeResponse(url));
        }
        catch (OAuthConfigurationException ex)
        {
            return OAuthProblemResults.FromGuide(ex.Guide);
        }
    }

    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] SearchConsoleCallbackRequest request)
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
    public async Task<IActionResult> Status()
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var connected = await _authService.IsConnectedAsync(customer.Id);
        if (!connected)
            return Ok(new SearchConsoleStatusResponse(false, []));

        var token = await _authService.GetAccessTokenAsync(customer.Id);
        if (token == null)
            return Ok(new SearchConsoleStatusResponse(false, []));

        var properties = await _apiClient.ListPropertiesAsync(token);
        var dtos = properties.Select(p => new SearchConsolePropertyDto(p.SiteUrl, p.PermissionLevel)).ToList();
        return Ok(new SearchConsoleStatusResponse(true, dtos));
    }

    [HttpDelete]
    public async Task<IActionResult> Disconnect()
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        await _authService.DisconnectAsync(customer.Id);
        return Ok(new { ok = true });
    }

    private async Task<Core.Domain.Customers.Customer?> GetCurrentCustomerAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var entityId)) return null;
        return await _customerService.GetCustomerByEntityIdAsync(entityId);
    }
}

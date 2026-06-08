using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Services.Notifications;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Web;

public record RegisterDeviceRequest(
    string Token,
    string Provider,      // "expo" | "fcm" | "apns"
    string Platform,      // "ios" | "android" | "web"
    string? DeviceName,
    string? AppVersion);

public record UnregisterDeviceRequest(string Token);

/// <summary>
/// Mobile cihazların push notification token'ını kaydeder.
/// Route: /api/web/devices/*
///
/// Mobile uygulama login olduktan sonra Expo'dan aldığı push token'ı
/// buraya gönderir. Logout'ta unregister çağırılır.
/// </summary>
public class DevicesController : WebApiController
{
    private readonly IDeviceTokenService _deviceTokens;
    private readonly ICustomerService _customers;

    public DevicesController(IDeviceTokenService deviceTokens, ICustomerService customers)
    {
        _deviceTokens = deviceTokens;
        _customers = customers;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest req)
    {
        var customerId = await GetCustomerIdAsync();
        if (customerId == 0) return Unauthorized();

        await _deviceTokens.RegisterAsync(
            customerId, req.Token, req.Provider, req.Platform,
            req.DeviceName, req.AppVersion);

        return Ok(new { ok = true });
    }

    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister([FromBody] UnregisterDeviceRequest req)
    {
        var customerId = await GetCustomerIdAsync();
        if (customerId == 0) return Unauthorized();

        await _deviceTokens.UnregisterAsync(customerId, req.Token);
        return Ok(new { ok = true });
    }

    private async Task<long> GetCustomerIdAsync()
    {
        // JWT "uid" claim'inde long Id var
        var uid = User.FindFirstValue("uid");
        if (long.TryParse(uid, out var id)) return id;

        // Fallback: sub (EntityId) ile lookup
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var entityId))
        {
            var customer = await _customers.GetCustomerByEntityIdAsync(entityId);
            return customer?.Id ?? 0;
        }
        return 0;
    }
}

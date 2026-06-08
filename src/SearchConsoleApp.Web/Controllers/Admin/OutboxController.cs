using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Auth;
using SearchConsoleApp.Core.Domain.Outbox;
using SearchConsoleApp.Data;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;
using SearchConsoleApp.Web.Framework.Auth;

namespace SearchConsoleApp.Web.Controllers.Admin;

public record OutboxMessageDto(
    long Id, Guid EntityId, string MessageType, string Target,
    string Status, int AttemptCount, DateTime CreatedOnUtc,
    DateTime? LastAttemptUtc, DateTime? CompletedUtc, string? LastError);

/// <summary>
/// Admin outbox monitoring + dead-letter yönetimi.
/// Route: /api/v1/admin/outbox/*
/// Permission: system.settings
/// </summary>
[HasPermission(Permissions.SystemSettings)]
public class OutboxController : AdminApiController
{
    private readonly IRepository<OutboxMessage> _repo;
    public OutboxController(IRepository<OutboxMessage> repo) => _repo = repo;

    /// <summary>Status filtreleyerek listele.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status = null,
                                          [FromQuery] int take = 100,
                                          [FromQuery] int skip = 0)
    {
        var query = _repo.Table.AsNoTracking();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(m => m.Status == status);

        var list = await query
            .OrderByDescending(m => m.CreatedOnUtc)
            .Skip(skip).Take(Math.Min(take, 500))
            .Select(m => new OutboxMessageDto(
                m.Id, m.EntityId, m.MessageType, m.Target,
                m.Status, m.AttemptCount, m.CreatedOnUtc,
                m.LastAttemptUtc, m.CompletedUtc, m.LastError))
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Dead-letter mesajı yeniden pending'e al.</summary>
    [HttpPost("{id:long}/retry")]
    [Audit("outbox.retry")]
    public async Task<IActionResult> Retry(long id)
    {
        var msg = await _repo.GetByIdAsync(id);
        if (msg == null) return NotFoundResult();
        if (msg.Status != "dead")
            return Problem(statusCode: 400, title: "Sadece dead-letter mesajlar retry edilebilir.");

        msg.Status = "pending";
        msg.AttemptCount = 0;
        msg.AvailableAtUtc = null;
        msg.LastError = null;
        msg.CompletedUtc = null;
        await _repo.UpdateAsync(msg, publishEvent: false);

        return Ok(new { ok = true });
    }

    /// <summary>Dead-letter mesajı sil (giderilemez ise).</summary>
    [HttpDelete("{id:long}")]
    [Audit("outbox.delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var msg = await _repo.GetByIdAsync(id);
        if (msg == null) return NotFoundResult();
        await _repo.HardDeleteAsync(msg, publishEvent: false);
        return Ok(new { ok = true });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public sealed class AuditIntegrationField
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public bool IsSecret { get; init; }
    public bool HasValue { get; init; }
    public string? MaskedValue { get; init; }
}

public sealed class AuditIntegrationItem
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Status { get; init; } = ""; // configured | missing | connected | not_connected | disabled
    public string? Detail { get; init; }
    public string? ConfigKey { get; init; }
    public bool Enabled { get; init; } = true;
    public bool CanToggle { get; init; } = true;
    public IList<AuditIntegrationField> Fields { get; init; } = [];
}

public interface IAuditIntegrationStatusService
{
    AuditIntegrationStatus GetGlobalStatus(bool searchConsoleConnected = false);
    Task<AuditRunIntegrationStatus?> GetRunStatusAsync(Guid auditRunEntityId, CancellationToken cancellationToken = default);
    bool IsIntegrationEnabled(string integrationId);
}

public sealed class AuditIntegrationStatus
{
    public IList<AuditIntegrationItem> Integrations { get; init; } = [];
}

public sealed class AuditRunIntegrationStatus
{
    public Guid AuditRunEntityId { get; init; }
    public string Mode { get; init; } = "";
    public IList<AuditIntegrationItem> Steps { get; init; } = [];
}

public partial class AuditIntegrationStatusService : IAuditIntegrationStatusService, IScopedService
{
    private readonly IConfiguration _config;
    private readonly IRepository<PageSpeedResult> _pageSpeedRepository;
    private readonly IRepository<IndexStatusSnapshot> _indexStatusRepository;
    private readonly IRepository<BacklinkSummary> _backlinkRepository;
    private readonly IRepository<SearchConsoleSnapshot> _scSnapshotRepository;
    private readonly IRepository<ContentQualityScore> _contentQualityRepository;
    private readonly IRepository<KeywordSerpSnapshot> _keywordSerpRepository;
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IIntegrationSettingsService _integrationSettings;

    public AuditIntegrationStatusService(
        IConfiguration config,
        IRepository<PageSpeedResult> pageSpeedRepository,
        IRepository<IndexStatusSnapshot> indexStatusRepository,
        IRepository<BacklinkSummary> backlinkRepository,
        IRepository<SearchConsoleSnapshot> scSnapshotRepository,
        IRepository<ContentQualityScore> contentQualityRepository,
        IRepository<KeywordSerpSnapshot> keywordSerpRepository,
        IRepository<AuditRun> auditRunRepository,
        IIntegrationSettingsService integrationSettings)
    {
        _config = config;
        _integrationSettings = integrationSettings;
        _pageSpeedRepository = pageSpeedRepository;
        _indexStatusRepository = indexStatusRepository;
        _backlinkRepository = backlinkRepository;
        _scSnapshotRepository = scSnapshotRepository;
        _contentQualityRepository = contentQualityRepository;
        _keywordSerpRepository = keywordSerpRepository;
        _auditRunRepository = auditRunRepository;
        _integrationSettings = integrationSettings;
    }

    public bool IsIntegrationEnabled(string integrationId) => _integrationSettings.IsEnabled(integrationId);

    public AuditIntegrationStatus GetGlobalStatus(bool searchConsoleConnected = false)
    {
        var settings = _integrationSettings.GetAll(searchConsoleConnected);
        var items = settings.Integrations.Select(i => new AuditIntegrationItem
        {
            Id = i.Id,
            Label = i.Label,
            Status = i.Status,
            Detail = i.Detail,
            ConfigKey = i.ConfigKey,
            Enabled = i.Enabled,
            CanToggle = i.CanToggle,
            Fields = i.Fields.Select(f => new AuditIntegrationField
            {
                Key = f.Key,
                Label = f.Label,
                IsSecret = f.IsSecret,
                HasValue = f.HasValue,
                MaskedValue = f.MaskedValue,
            }).ToList(),
        }).ToList();

        return new AuditIntegrationStatus { Integrations = items };
    }

    public async Task<AuditRunIntegrationStatus?> GetRunStatusAsync(
        Guid auditRunEntityId, CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(auditRunEntityId);
        if (run == null) return null;

        var runId = run.Id;
        var pageSpeed = await _pageSpeedRepository.Table.AnyAsync(p => p.AuditRunId == runId, cancellationToken);
        var indexSnap = await _indexStatusRepository.Table.FirstOrDefaultAsync(i => i.AuditRunId == runId, cancellationToken);
        var backlinks = await _backlinkRepository.Table.FirstOrDefaultAsync(b => b.AuditRunId == runId, cancellationToken);
        var scSnap = await _scSnapshotRepository.Table.FirstOrDefaultAsync(s => s.AuditRunId == runId, cancellationToken);
        var cq = await _contentQualityRepository.Table.AnyAsync(c => c.AuditRunId == runId, cancellationToken);
        var serp = await _keywordSerpRepository.Table.AnyAsync(k => k.AuditRunId == runId, cancellationToken);

        var steps = new List<AuditIntegrationItem>
        {
            Step("crawl", "Site taraması", run.PagesCrawled > 0 ? "ran" : "pending", $"{run.PagesCrawled} sayfa"),
            Step("pagespeed", "PageSpeed / CWV", pageSpeed ? "ran" : !IsIntegrationEnabled("pagespeed") ? "skipped" : IsConfigured("Google:PageSpeedApiKey") ? "skipped" : "not_configured",
                pageSpeed ? "Veri kaydedildi" : !IsIntegrationEnabled("pagespeed") ? "Devre dışı" : "API anahtarı yok veya sayfa seçilmedi"),
            Step("index", "İndeks durumu", indexSnap != null ? "ran" : "skipped",
                indexSnap?.Source ?? "Custom Search veya tahmin"),
            Step("backlinks", "Link profili", backlinks != null ? "ran" : "skipped",
                backlinks != null ? $"{backlinks.InternalLinkCount} iç link" : null),
            Step("content-quality", "E-E-A-T (LLM)", cq ? "ran" : !IsIntegrationEnabled("llm-eeat") ? "skipped" : IsConfigured("Llm:ApiKey") ? "skipped" : "not_configured",
                !IsIntegrationEnabled("llm-eeat") ? "Devre dışı" : null),
            Step("keyword-serp", "Anahtar kelime SERP", serp ? "ran" : "skipped", "Bağlı mod + watch + Custom Search gerekir"),
        };

        if (run.Mode == AuditMode.Connected)
        {
            steps.Add(Step("search-console", "Search Console", scSnap != null ? "ran" : "skipped",
                scSnap != null ? "Snapshot kaydedildi" : "OAuth veya property eksik"));
        }

        return new AuditRunIntegrationStatus
        {
            AuditRunEntityId = auditRunEntityId,
            Mode = run.Mode.ToString(),
            Steps = steps,
        };
    }

    private static AuditIntegrationItem Step(string id, string label, string status, string? detail)
        => new() { Id = id, Label = label, Status = status, Detail = detail };

    private bool IsConfigured(string key) => !string.IsNullOrWhiteSpace(_config[key]);
}

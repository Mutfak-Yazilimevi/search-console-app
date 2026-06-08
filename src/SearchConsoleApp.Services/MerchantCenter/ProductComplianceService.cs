using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Audit;

namespace SearchConsoleApp.Services.MerchantCenter;

public partial class ProductComplianceService : IProductComplianceService, IScopedService
{
    private readonly IRepository<ProductComplianceRun> _runRepo;
    private readonly IRepository<ProductComplianceItem> _itemRepo;
    private readonly IRepository<ProductComplianceIssue> _issueRepo;
    private readonly IGmcSpecValidator _validator;
    private readonly IMerchantCenterAuthService _gmcAuth;
    private readonly IMerchantCenterApiClient _gmcApi;
    private readonly IGmcProductMergeService _gmcMerge;
    private readonly IGmcPageSpeedChecker _pageSpeedChecker;
    private readonly IGmcComplianceReportService _reportService;
    private readonly IGmcSafeBrowsingChecker _safeBrowsingChecker;
    private readonly IGmcComplianceDiffService _diffService;
    private readonly ICrawlWorkerClient _crawlWorkerClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ProductComplianceService> _logger;

    public ProductComplianceService(
        IRepository<ProductComplianceRun> runRepo,
        IRepository<ProductComplianceItem> itemRepo,
        IRepository<ProductComplianceIssue> issueRepo,
        IGmcSpecValidator validator,
        IMerchantCenterAuthService gmcAuth,
        IMerchantCenterApiClient gmcApi,
        IGmcProductMergeService gmcMerge,
        IGmcPageSpeedChecker pageSpeedChecker,
        IGmcComplianceReportService reportService,
        IGmcSafeBrowsingChecker safeBrowsingChecker,
        IGmcComplianceDiffService diffService,
        ICrawlWorkerClient crawlWorkerClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<ProductComplianceService> logger)
    {
        _runRepo = runRepo;
        _itemRepo = itemRepo;
        _issueRepo = issueRepo;
        _validator = validator;
        _gmcAuth = gmcAuth;
        _gmcApi = gmcApi;
        _gmcMerge = gmcMerge;
        _pageSpeedChecker = pageSpeedChecker;
        _reportService = reportService;
        _safeBrowsingChecker = safeBrowsingChecker;
        _diffService = diffService;
        _crawlWorkerClient = crawlWorkerClient;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<ProductComplianceRun> StartAsync(
        string url,
        long? customerId,
        string? merchantCenterAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required.", nameof(url));

        var normalized = AuditUrlNormalizer.Normalize(url);
        var mode = ProductComplianceAnalysisMode.SiteOnly;
        string? resolvedAccountId = merchantCenterAccountId;

        if (customerId.HasValue)
        {
            var connected = await _gmcAuth.IsConnectedAsync(customerId.Value);
            if (connected)
            {
                if (string.IsNullOrWhiteSpace(resolvedAccountId))
                {
                    var token = await _gmcAuth.GetAccessTokenAsync(customerId.Value, cancellationToken);
                    if (token != null)
                    {
                        var accounts = await _gmcApi.ListAccountsAsync(token, cancellationToken);
                        resolvedAccountId = _gmcApi.FindMatchingAccount(accounts, normalized);
                    }
                }

                if (!string.IsNullOrWhiteSpace(resolvedAccountId))
                    mode = ProductComplianceAnalysisMode.GmcConnected;
            }
        }

        var run = new ProductComplianceRun
        {
            InputUrl = url.Trim(),
            NormalizedUrl = normalized,
            Status = ProductComplianceRunStatus.Pending,
            AnalysisMode = mode,
            CustomerId = customerId,
            MerchantCenterAccountId = resolvedAccountId,
            CreatedAt = DateTime.UtcNow,
        };

        await _runRepo.InsertAsync(run, publishEvent: false);
        await EnqueueCrawlAsync(run, cancellationToken);
        return run;
    }

    public async Task ProcessProductAsync(
        Guid runEntityId,
        ProductComplianceCrawlProductPayload payload,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || IsTerminal(run.Status)) return;

        var exists = await _itemRepo.Table.AnyAsync(
            i => i.RunId == run.Id && i.PageUrl == payload.Url, cancellationToken);
        if (exists) return;

        var item = new ProductComplianceItem
        {
            RunId = run.Id,
            PageUrl = payload.Url,
            Title = payload.Title,
            ExtractedDataJson = payload.ExtractedProductJson,
            CreatedAt = DateTime.UtcNow,
        };

        await _itemRepo.InsertAsync(item, publishEvent: false);

        run.Status = ProductComplianceRunStatus.Crawling;
        run.ProgressPhase = "crawling";
        run.ProgressMessage = $"Ürün taranıyor… ({run.TotalProducts + 1})";
        run.TotalProducts++;
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    public async Task CompleteCrawlAsync(
        Guid runEntityId,
        ProductComplianceCrawlCompletePayload payload,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || IsTerminal(run.Status)) return;

        run.Status = ProductComplianceRunStatus.Analyzing;
        run.ProgressPhase = "analyzing";
        run.ProgressMessage = "Ürün uyumluluğu analiz ediliyor…";
        run.SiteCheckHtml = payload.SiteCheckHtml;
        await _runRepo.UpdateAsync(run, publishEvent: false);

        await FinalizeAnalysisAsync(run, cancellationToken);
    }

    public async Task FailCrawlAsync(
        Guid runEntityId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || run.Status == ProductComplianceRunStatus.Cancelled) return;
        if (IsTerminal(run.Status)) return;

        run.Status = ProductComplianceRunStatus.Failed;
        run.ErrorMessage = errorMessage;
        run.CompletedAt = DateTime.UtcNow;
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    public async Task CancelAsync(Guid runEntityId, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || IsTerminal(run.Status)) return;

        run.Status = ProductComplianceRunStatus.Cancelled;
        run.ProgressPhase = "cancelled";
        run.ProgressMessage = "Analiz iptal edildi.";
        run.CompletedAt = DateTime.UtcNow;
        await _runRepo.UpdateAsync(run, publishEvent: false);
        await _crawlWorkerClient.CancelProductComplianceJobAsync(runEntityId, cancellationToken);
    }

    public async Task RescanProductAsync(
        Guid runEntityId,
        Guid productEntityId,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || run.Status != ProductComplianceRunStatus.Completed)
            throw new InvalidOperationException("Yalnızca tamamlanmış analizlerde ürün yeniden taranabilir.");

        var item = await _itemRepo.GetByEntityIdAsync(productEntityId);
        if (item == null || item.RunId != run.Id)
            throw new InvalidOperationException("Ürün bulunamadı.");

        run.ProgressPhase = "rescanning";
        run.ProgressMessage = $"Ürün yeniden taranıyor… ({item.Title ?? item.PageUrl})";
        await _runRepo.UpdateAsync(run, publishEvent: false);

        var workerUrl = _config["Audit:CrawlWorkerUrl"];
        if (string.IsNullOrWhiteSpace(workerUrl))
            throw new InvalidOperationException("Crawl worker yapılandırılmamış.");

        var payload = new
        {
            productComplianceRunEntityId = run.EntityId,
            productItemEntityId = item.EntityId,
            url = item.PageUrl,
        };

        var client = _httpClientFactory.CreateClient("audit-crawl");
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(
            $"{workerUrl.TrimEnd('/')}/enqueue-product-rescan",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            run.ProgressPhase = "completed";
            run.ProgressMessage = "Analiz tamamlandı";
            await _runRepo.UpdateAsync(run, publishEvent: false);
            throw new InvalidOperationException("Ürün tarama işi kuyruğa eklenemedi.");
        }
    }

    public async Task FailProductRescanAsync(
        Guid runEntityId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null) return;

        run.ProgressPhase = "completed";
        run.ProgressMessage = errorMessage;
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    public async Task ProcessProductRescanAsync(
        Guid runEntityId,
        Guid productItemEntityId,
        ProductComplianceCrawlProductPayload payload,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || run.ProgressPhase != "rescanning") return;

        var item = await _itemRepo.GetByEntityIdAsync(productItemEntityId);
        if (item == null || item.RunId != run.Id) return;

        item.Title = payload.Title;
        item.ExtractedDataJson = payload.ExtractedProductJson;
        await _itemRepo.UpdateAsync(item, publishEvent: false);

        run.ProgressMessage = "Ürün yeniden analiz ediliyor…";
        await _runRepo.UpdateAsync(run, publishEvent: false);

        await ReanalyzeProductAsync(run, item, cancellationToken);
    }

    public async Task CompleteProductRescanAsync(
        Guid runEntityId,
        Guid productItemEntityId,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || run.ProgressPhase != "rescanning") return;

        var item = await _itemRepo.GetByEntityIdAsync(productItemEntityId);
        if (item == null || item.RunId != run.Id) return;

        await RecalculateRunTotalsAsync(run, cancellationToken);

        run.ProgressPhase = "completed";
        run.ProgressMessage = "Ürün yeniden tarama tamamlandı";
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }
}

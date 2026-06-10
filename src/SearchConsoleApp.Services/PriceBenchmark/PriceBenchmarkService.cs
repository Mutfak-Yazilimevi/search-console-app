using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.PriceBenchmark;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Audit;

namespace SearchConsoleApp.Services.PriceBenchmark;

public partial class PriceBenchmarkService : IPriceBenchmarkService, IScopedService
{
    private readonly IRepository<PriceBenchmarkRun> _runRepo;
    private readonly IRepository<PriceBenchmarkItem> _itemRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<PriceBenchmarkService> _logger;

    public PriceBenchmarkService(
        IRepository<PriceBenchmarkRun> runRepo,
        IRepository<PriceBenchmarkItem> itemRepo,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<PriceBenchmarkService> logger)
    {
        _runRepo = runRepo;
        _itemRepo = itemRepo;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<PriceBenchmarkRun> StartAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required.", nameof(url));

        var normalized = AuditUrlNormalizer.Normalize(url);
        var run = new PriceBenchmarkRun
        {
            InputUrl = url.Trim(),
            NormalizedUrl = normalized,
            Status = PriceBenchmarkRunStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProgressPhase = "queued",
            ProgressMessage = "Fiyat karşılaştırması kuyruğa alındı…",
        };

        await _runRepo.InsertAsync(run, publishEvent: false);
        await EnqueueCrawlAsync(run, cancellationToken);
        return run;
    }

    public async Task<PriceBenchmarkDetailDto?> GetDetailAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(entityId);
        if (run == null) return null;

        var products = await OrderProductsQuery(_itemRepo.Table.Where(i => i.RunId == run.Id), run)
            .Take(200)
            .ToListAsync(cancellationToken);

        return new PriceBenchmarkDetailDto(
            PriceBenchmarkMapper.MapRun(run),
            products.Select(PriceBenchmarkMapper.MapItem).ToList());
    }

    public async Task<IList<PriceBenchmarkItemDto>> GetProductsAsync(
        Guid entityId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(entityId);
        if (run == null) return [];

        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var items = await OrderProductsQuery(_itemRepo.Table.Where(i => i.RunId == run.Id), run)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return items.Select(PriceBenchmarkMapper.MapItem).ToList();
    }

    public async Task ProcessDiscoveredProductAsync(
        Guid runEntityId, PriceBenchmarkProductPayload payload, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId)
            ?? throw new InvalidOperationException($"Price benchmark run {runEntityId} not found.");

        if (run.Status == PriceBenchmarkRunStatus.Cancelled || IsTerminal(run.Status))
            return;

        var pageUrl = payload.Url ?? "";
        var exists = await _itemRepo.Table.AnyAsync(
            i => i.RunId == run.Id && i.PageUrl == pageUrl, cancellationToken);

        if (!exists)
        {
            await _itemRepo.InsertAsync(new PriceBenchmarkItem
            {
                RunId = run.Id,
                PageUrl = pageUrl,
                Title = payload.Title,
                OurPrice = payload.OurPrice,
                PriceCurrency = payload.PriceCurrency ?? "TRY",
                ExtractedDataJson = payload.ExtractedProductJson ?? "{}",
                ShoppingOffersJson = "[]",
                CreatedAt = DateTime.UtcNow,
            }, publishEvent: false);

            run.TotalProducts++;
        }

        run.Status = PriceBenchmarkRunStatus.Crawling;
        run.ProgressPhase = payload.ProgressPhase ?? "discovering";
        run.ProgressMessage = payload.ProgressMessage ?? $"{run.TotalProducts} ürün bulundu";
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    public async Task ProcessDiscoverCompleteAsync(
        Guid runEntityId, PriceBenchmarkProductPayload payload, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || run.Status == PriceBenchmarkRunStatus.Cancelled || IsTerminal(run.Status))
            return;

        run.Status = PriceBenchmarkRunStatus.Comparing;
        run.ProgressPhase = payload.ProgressPhase ?? "discover-complete";
        run.ProgressMessage = payload.ProgressMessage
            ?? $"{run.TotalProducts} ürün bulundu — fiyat karşılaştırması başlıyor…";
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    public async Task ProcessComparedProductAsync(
        Guid runEntityId, PriceBenchmarkProductPayload payload, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId)
            ?? throw new InvalidOperationException($"Price benchmark run {runEntityId} not found.");

        if (run.Status == PriceBenchmarkRunStatus.Cancelled || IsTerminal(run.Status))
            return;

        var pageUrl = payload.Url ?? "";
        var item = await _itemRepo.Table.FirstOrDefaultAsync(
            i => i.RunId == run.Id && i.PageUrl == pageUrl, cancellationToken);

        if (item == null)
        {
            await _itemRepo.InsertAsync(new PriceBenchmarkItem
            {
                RunId = run.Id,
                PageUrl = pageUrl,
                Title = payload.Title,
                OurPrice = payload.OurPrice,
                PriceCurrency = payload.PriceCurrency ?? "TRY",
                MinMarketPrice = payload.MinMarketPrice,
                MaxMarketPrice = payload.MaxMarketPrice,
                WeightedAvgMarketPrice = payload.WeightedAvgMarketPrice,
                MinOfferLink = payload.MinOfferLink,
                MinOfferSource = payload.MinOfferSource,
                MaxOfferLink = payload.MaxOfferLink,
                MaxOfferSource = payload.MaxOfferSource,
                MarketOfferCount = payload.MarketOfferCount ?? 0,
                DeltaPercent = payload.DeltaPercent,
                MarketPosition = PriceBenchmarkMapper.ParsePosition(payload.MarketPosition),
                ShoppingError = payload.ShoppingError,
                ExtractedDataJson = payload.ExtractedProductJson ?? "{}",
                ShoppingOffersJson = payload.ShoppingOffersJson ?? "[]",
                CreatedAt = DateTime.UtcNow,
            }, publishEvent: false);
            run.TotalProducts++;
        }
        else
        {
            item.Title = payload.Title ?? item.Title;
            item.OurPrice = payload.OurPrice ?? item.OurPrice;
            item.PriceCurrency = payload.PriceCurrency ?? item.PriceCurrency;
            item.MinMarketPrice = payload.MinMarketPrice;
            item.MaxMarketPrice = payload.MaxMarketPrice;
            item.WeightedAvgMarketPrice = payload.WeightedAvgMarketPrice;
            item.MinOfferLink = payload.MinOfferLink;
            item.MinOfferSource = payload.MinOfferSource;
            item.MaxOfferLink = payload.MaxOfferLink;
            item.MaxOfferSource = payload.MaxOfferSource;
            item.MarketOfferCount = payload.MarketOfferCount ?? 0;
            item.DeltaPercent = payload.DeltaPercent;
            item.MarketPosition = PriceBenchmarkMapper.ParsePosition(payload.MarketPosition);
            item.ShoppingError = payload.ShoppingError;
            if (!string.IsNullOrWhiteSpace(payload.ExtractedProductJson))
                item.ExtractedDataJson = payload.ExtractedProductJson;
            item.ShoppingOffersJson = payload.ShoppingOffersJson ?? "[]";
            await _itemRepo.UpdateAsync(item, publishEvent: false);
        }

        run.Status = PriceBenchmarkRunStatus.Comparing;
        run.ProgressPhase = payload.ProgressPhase ?? "comparing";
        run.ProgressMessage = payload.ProgressMessage ?? "Fiyat karşılaştırması devam ediyor…";
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    public Task ProcessProductAsync(
        Guid runEntityId, PriceBenchmarkProductPayload payload, CancellationToken cancellationToken = default)
        => ProcessComparedProductAsync(runEntityId, payload, cancellationToken);

    public async Task CompleteAsync(
        Guid runEntityId, PriceBenchmarkCompletePayload payload, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || IsTerminal(run.Status)) return;

        run.Status = PriceBenchmarkRunStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        run.TotalProducts = Math.Max(run.TotalProducts, payload.TotalProducts);
        run.SerpApiConfigured = payload.SerpApiConfigured;
        run.ProgressPhase = "completed";
        run.ProgressMessage = payload.SerpApiConfigured
            ? $"Tamamlandı — {run.TotalProducts} ürün karşılaştırıldı"
            : $"Tamamlandı — Google Shopping taraması yapılandırılmamış, piyasa fiyatları alınamadı";
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    public async Task FailAsync(Guid runEntityId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || run.Status == PriceBenchmarkRunStatus.Cancelled || IsTerminal(run.Status))
            return;

        run.Status = PriceBenchmarkRunStatus.Failed;
        run.ErrorMessage = errorMessage;
        run.CompletedAt = DateTime.UtcNow;
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    public async Task CancelAsync(Guid runEntityId, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null || IsTerminal(run.Status)) return;

        run.Status = PriceBenchmarkRunStatus.Cancelled;
        run.ProgressPhase = "cancelled";
        run.ProgressMessage = "Analiz iptal edildi.";
        run.CompletedAt = DateTime.UtcNow;
        await _runRepo.UpdateAsync(run, publishEvent: false);
        await CancelWorkerJobAsync(runEntityId, cancellationToken);
    }

    private async Task EnqueueCrawlAsync(PriceBenchmarkRun run, CancellationToken cancellationToken)
    {
        var workerUrl = _config["Audit:CrawlWorkerUrl"];
        if (string.IsNullOrWhiteSpace(workerUrl))
        {
            run.Status = PriceBenchmarkRunStatus.Failed;
            run.ErrorMessage = "Crawl worker yapılandırılmamış.";
            run.CompletedAt = DateTime.UtcNow;
            await _runRepo.UpdateAsync(run, publishEvent: false);
            return;
        }

        var maxProducts = _config.GetValue("PriceBenchmark:MaxProducts", 100);
        var payload = new
        {
            priceBenchmarkRunEntityId = run.EntityId,
            url = run.NormalizedUrl,
            maxProducts,
        };

        var client = _httpClientFactory.CreateClient("audit-crawl");
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{workerUrl.TrimEnd('/')}/enqueue-price-benchmark", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await FailAsync(run.EntityId, "Fiyat karşılaştırma işi kuyruğa eklenemedi.", cancellationToken);
            return;
        }

        run.Status = PriceBenchmarkRunStatus.Crawling;
        run.StartedAt = DateTime.UtcNow;
        run.ProgressPhase = "crawling";
        run.ProgressMessage = "Ürünler sitemap ve site üzerinden taranıyor…";
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    private async Task CancelWorkerJobAsync(Guid runEntityId, CancellationToken cancellationToken)
    {
        var workerUrl = _config["Audit:CrawlWorkerUrl"];
        if (string.IsNullOrWhiteSpace(workerUrl)) return;

        try
        {
            var client = _httpClientFactory.CreateClient("audit-crawl");
            var json = JsonSerializer.Serialize(new { priceBenchmarkRunEntityId = runEntityId });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await client.PostAsync($"{workerUrl.TrimEnd('/')}/cancel", content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Price benchmark cancel worker call failed for {EntityId}", runEntityId);
        }
    }

    private static IQueryable<PriceBenchmarkItem> OrderProductsQuery(
        IQueryable<PriceBenchmarkItem> query, PriceBenchmarkRun run) =>
        run.Status == PriceBenchmarkRunStatus.Completed
            ? query.OrderBy(i => i.DeltaPercent ?? 0)
            : query.OrderBy(i => i.CreatedAt);

    private static bool IsTerminal(PriceBenchmarkRunStatus status) =>
        status is PriceBenchmarkRunStatus.Completed
            or PriceBenchmarkRunStatus.Failed
            or PriceBenchmarkRunStatus.Cancelled;
}
